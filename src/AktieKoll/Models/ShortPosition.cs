using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class ShortPosition
{
    public int Id { get; set; }

    [MaxLength(20)]
    public string? Lei { get; set; }

    [MaxLength(300)]
    public required string HolderName { get; set; }

    [MaxLength(200)]
    public required string IssuerName { get; set; }

    [MaxLength(12)]
    public required string Isin { get; set; }

    [Column(TypeName = "numeric(6,2)")]
    public decimal PositionPercent { get; set; }

    public bool BelowThreshold { get; set; }

    [Column(TypeName = "date")]
    public DateTime PositionDate { get; set; }

    /// <summary>True while this row appears in FI's current-positions file (the authoritative list of open positions).</summary>
    public bool InCurrentFile { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }
}
