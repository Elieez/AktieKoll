using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class ShortSnapshot
{
    public int Id { get; set; }

    [MaxLength(20)]
    public required string Lei { get; set; }

    [Column(TypeName = "date")]
    public DateTime SnapshotDate { get; set; }

    [Column(TypeName = "numeric(6,2)")]
    public decimal PositionPercent { get; set; }

    [Column(TypeName = "date")]
    public DateTime? LatestPositionDate { get; set; }
}
