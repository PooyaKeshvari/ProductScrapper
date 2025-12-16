using PriceMonitor.Models;
using PriceMonitor.Settings;

namespace PriceMonitor.Services.Scraping;

public record ScrapeResult(
    bool Success,
    string? Title,
    decimal? Price,
    string Currency,
    string? Availability,
    double Confidence,
    string? EvidenceHtml,
    string? ErrorMessage
);

public interface ISeleniumScraper
{
    Task<ScrapeResult> ExecuteAsync(Product product, WebsiteRule rule, CancellationToken cancellationToken);
}
