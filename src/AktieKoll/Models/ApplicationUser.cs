using Microsoft.AspNetCore.Identity;

namespace AktieKoll.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;
    public string? TenantId { get; set; } 
}
