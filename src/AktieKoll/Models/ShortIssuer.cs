using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class ShortIssuer
{
    [Key]
    [MaxLength(20)]
    public string Lei { get; set; } = string.Empty;

    [MaxLength(200)]
    public required string Name { get; set; }

    [MaxLength(20)]
    public string? PrimarySymbol { get; set; }

    [Column(TypeName = "timestamp with time zone")]
    public DateTime? GleifCheckedAt { get; set; }
}
