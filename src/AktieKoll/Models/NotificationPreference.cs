using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

public class NotificationPreference
{
    public int Id { get; set; }

    [Required]
    public required string UserId { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool DiscordEnabled { get; set; } = false;

    [MaxLength(500)]
    public string? DiscordWebhookUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = null!;
}