using PriceMonitor.Models;
using PriceMonitor.Services.Scraping;

namespace PriceMonitor.Services.Validation;

public interface IPriceValidator
{
    (bool isValid, double confidence, string? reason) Validate(Product product, ScrapeResult result);
}
