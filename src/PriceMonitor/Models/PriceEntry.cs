using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PriceMonitor.Models;

public class PriceEntry
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    [MaxLength(64)]
    public string Website { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(8)]
    public string Currency { get; set; } = "IRR";

    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;

    [Range(0, 1)]
    public double Confidence { get; set; }

    public string? Availability { get; set; }

    public string? EvidenceHtml { get; set; }
}
