using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

public class Company
{
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public required string Code { get; set; } // Ticker symbol

    [Required]
    [MaxLength(200)]
    public required string Name { get; set; } // Company name

    [MaxLength(12)]
    public string? Isin { get; set; }

    [MaxLength(10)]
    public string? Currency { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}