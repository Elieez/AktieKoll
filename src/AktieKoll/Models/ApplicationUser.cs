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
}