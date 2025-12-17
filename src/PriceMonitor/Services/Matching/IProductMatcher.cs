using PriceMonitor.Models;

namespace PriceMonitor.Services.Matching;

public interface IProductMatcher
{
    double EvaluateMatch(Product product, string scrapedTitle);
}
