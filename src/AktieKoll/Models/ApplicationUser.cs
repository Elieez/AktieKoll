using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(50)]
    public string? DisplayName { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeleted { get; set; } = false;
    public string? TenantId { get; set; }

    // Google OAuth
    public string? GoogleId { get; set; }
    public string? GoogleAvatarUrl { get; set; }
    public string? GoogleDisplayName { get; set; }

    // Account deletion (two-step, 1-hour token)
    public string? DeletionTokenHash { get; set; }
    public DateTime? DeletionTokenExpiresAt { get; set; }
}
