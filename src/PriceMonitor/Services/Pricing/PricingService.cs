using PriceMonitor.Models;

namespace PriceMonitor.Services.Pricing;

public class PricingService : IPricingService
{
    public decimal? CalculateAverage(IEnumerable<PriceEntry> entries)
    {
        var prices = entries.Select(p => p.Price).ToList();
        if (!prices.Any()) return null;
        return prices.Average();
    }

    public PriceRanking Rank(decimal? internalPrice, IEnumerable<PriceEntry> entries)
    {
        var ordered = entries.OrderBy(p => p.Price).ToList();
        if (!ordered.Any())
        {
            return new PriceRanking(internalPrice, null, null, null, 0);
        }

        var average = ordered.Average(p => p.Price);
        var min = ordered.First().Price;
        var max = ordered.Last().Price;
        var position = 1;
        if (internalPrice.HasValue)
        {
            position = ordered.Count(p => p.Price <= internalPrice.Value) + 1;
        }

        return new PriceRanking(internalPrice, average, min, max, position);
    }
}
