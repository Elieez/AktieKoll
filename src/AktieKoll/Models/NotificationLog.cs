using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AktieKoll.Models;

public class NotificationLog
{
    public int Id { get; set; }

    [Required]
    public required string UserId { get; set; }

    public int CompanyId { get; set; }

    [Required]
    [MaxLength(50)]
    public required string BatchRunId { get; set; }

    public int[] TransactionIds { get; set; } = [];

    [Required]
    [MaxLength(20)]
    public required string Channel { get; set; } // "email" or "discord"

    [Column(TypeName = "timestamp with time zone")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}