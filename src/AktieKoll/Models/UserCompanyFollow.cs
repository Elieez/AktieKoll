using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

public class UserCompanyFollow
{
    public int Id { get; set; }

    [Required]
    public required string UserId { get; set; }

    public int CompanyId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ApplicationUser User { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
