using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class InsiderTrade
{
    public int Id { get; set; }
    public required string CompanyName { get; set; }
    public required string InsiderName { get; set; }
    public string? Position { get; set; }
    public required string TransactionType { get; set; }
    public required long Shares { get; set; }
    public required decimal Price { get; set; }
    public required string Currency { get; set; }
    public string? Isin { get; set; }
    public string? Symbol { get; set; }
    public required string Status { get; set; }

    // Extra fields from FI's register (null for rows stored before they were captured).
    [MaxLength(20)]
    public string? Lei { get; set; }
    [MaxLength(100)]
    public string? InstrumentType { get; set; }
    [MaxLength(200)]
    public string? TradingVenue { get; set; }
    /// <summary>Reported by a closely associated person/entity (FI: "Närstående").</summary>
    public bool? IsCloselyAssociated { get; set; }
    /// <summary>Linked to a share/incentive program (FI: "Är kopplad till aktieprogram").</summary>
    public bool? IsShareProgram { get; set; }

    /// <summary>Trade value (shares × price) converted to SEK. Null when no FX rate was available.</summary>
    [Column(TypeName = "numeric(20,2)")]
    public decimal? ValueSek { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public required DateTime PublishingDate { get; set; }
    [Column(TypeName = "date")]
    public required DateTime TransactionDate { get; set; }
}