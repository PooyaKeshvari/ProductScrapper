using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PriceMonitor.Data;
using PriceMonitor.Models;
using PriceMonitor.Services.Validation;
using PriceMonitor.Settings;

namespace PriceMonitor.Services.Scraping;

public class ScrapeOrchestrator : IScrapeOrchestrator
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISeleniumScraper _seleniumScraper;
    private readonly IPriceValidator _validator;
    private readonly ScraperSettings _settings;

    public ScrapeOrchestrator(ApplicationDbContext dbContext, ISeleniumScraper seleniumScraper, IPriceValidator validator, IOptionsSnapshot<ScraperSettings> settings)
    {
        _dbContext = dbContext;
        _seleniumScraper = seleniumScraper;
        _validator = validator;
        _settings = settings.Value;
    }

    public async Task<ScrapeOutcome> RunForProductAsync(Product product, IEnumerable<WebsiteRule> websites, CancellationToken cancellationToken)
    {
        var savedEntries = new List<PriceEntry>();
        var runs = new List<ScrapeRun>();

        foreach (var site in websites.Where(w => w.Enabled))
        {
            var run = new ScrapeRun
            {
                ProductId = product.Id,
                Website = site.Name,
                StartedAt = DateTime.UtcNow,
            };

            runs.Add(run);
            _dbContext.Runs.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = await _seleniumScraper.ExecuteAsync(product, site, cancellationToken);
            var (isValid, confidence, reason) = _validator.Validate(product, result);

            run.CompletedAt = DateTime.UtcNow;
            run.Success = isValid;
            run.ErrorMessage = isValid ? null : reason ?? result.ErrorMessage;

            if (isValid && result.Price.HasValue)
            {
                var priceEntry = new PriceEntry
                {
                    ProductId = product.Id,
                    Website = site.Name,
                    Price = result.Price.Value,
                    Currency = site.Currency ?? "IRR",
                    RetrievedAt = DateTime.UtcNow,
                    Confidence = confidence,
                    Availability = result.Availability,
                    EvidenceHtml = result.EvidenceHtml
                };
                savedEntries.Add(priceEntry);
                _dbContext.Prices.Add(priceEntry);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ScrapeOutcome(savedEntries, runs);
    }
}
