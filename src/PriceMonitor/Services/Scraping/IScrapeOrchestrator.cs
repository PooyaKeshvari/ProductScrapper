using PriceMonitor.Models;
using PriceMonitor.Settings;

namespace PriceMonitor.Services.Scraping;

public interface IScrapeOrchestrator
{
    Task<ScrapeOutcome> RunForProductAsync(Product product, IEnumerable<WebsiteRule> websites, CancellationToken cancellationToken);
}

public record ScrapeOutcome(List<PriceEntry> SavedEntries, List<ScrapeRun> Runs);
