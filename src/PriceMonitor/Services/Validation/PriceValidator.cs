using PriceMonitor.Models;
using PriceMonitor.Services.Matching;
using PriceMonitor.Services.Scraping;

namespace PriceMonitor.Services.Validation;

public class PriceValidator : IPriceValidator
{
    private readonly IProductMatcher _matcher;

    public PriceValidator(IProductMatcher matcher)
    {
        _matcher = matcher;
    }

    public (bool isValid, double confidence, string? reason) Validate(Product product, ScrapeResult result)
    {
        if (!result.Success || result.Price is null)
        {
            return (false, 0, result.ErrorMessage ?? "Scrape failed");
        }

        var matchConfidence = _matcher.EvaluateMatch(product, result.Title ?? string.Empty);
        var priceWithinBounds = product.InternalPrice is null || (result.Price > 0 && result.Price < product.InternalPrice * 4);

        if (!priceWithinBounds)
        {
            return (false, matchConfidence * 0.5, "Price out of expected range");
        }

        var combinedConfidence = Math.Min(1, matchConfidence * 0.7 + result.Confidence * 0.3);
        var isValid = combinedConfidence >= 0.3;

        return (isValid, combinedConfidence, isValid ? null : "Low confidence");
    }
}
