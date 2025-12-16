using System.ComponentModel.DataAnnotations;

namespace PriceMonitor.Models;

public class Product
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Sku { get; set; }

    [MaxLength(128)]
    public string? Brand { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? InternalPrice { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<PriceEntry> Prices { get; set; } = new();
    public List<ScrapeRun> Runs { get; set; } = new();
}
