using PriceMonitor.Models;

namespace PriceMonitor.Services.Pricing;

public interface IPricingService
{
    decimal? CalculateAverage(IEnumerable<PriceEntry> entries);
    PriceRanking Rank(decimal? internalPrice, IEnumerable<PriceEntry> entries);
}

public record PriceRanking(decimal? InternalPrice, decimal? MarketAverage, decimal? MinPrice, decimal? MaxPrice, int Position);
