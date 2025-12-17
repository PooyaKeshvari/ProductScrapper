using System.ComponentModel.DataAnnotations;

namespace PriceMonitor.Models;

public class CompetitorProductMatch
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [MaxLength(64)]
    public string Website { get; set; } = string.Empty;

    [MaxLength(512)]
    public string CompetitorTitle { get; set; } = string.Empty;

    [Url]
    public string? Url { get; set; }

    [Range(0, 1)]
    public double Confidence { get; set; }

    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}
