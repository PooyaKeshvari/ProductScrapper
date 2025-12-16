using System.Globalization;
using PriceMonitor.Models;

namespace PriceMonitor.Services.Matching;

public class ProductMatcher : IProductMatcher
{
    public double EvaluateMatch(Product product, string scrapedTitle)
    {
        if (string.IsNullOrWhiteSpace(scrapedTitle)) return 0;

        var normalizedTitle = scrapedTitle.ToLower(CultureInfo.InvariantCulture);
        var nameScore = normalizedTitle.Contains(product.Name.ToLower(CultureInfo.InvariantCulture)) ? 0.6 : 0.2;
        var brandScore = !string.IsNullOrWhiteSpace(product.Brand) && normalizedTitle.Contains(product.Brand!.ToLower(CultureInfo.InvariantCulture)) ? 0.3 : 0.05;
        var skuScore = !string.IsNullOrWhiteSpace(product.Sku) && normalizedTitle.Contains(product.Sku!.ToLower(CultureInfo.InvariantCulture)) ? 0.2 : 0;

        return Math.Min(1, nameScore + brandScore + skuScore);
    }
}
