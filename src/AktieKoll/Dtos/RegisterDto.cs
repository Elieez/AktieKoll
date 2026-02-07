using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record RegisterDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string Password { get; init; }

    [MaxLength(50)]
    public string? DisplayName { get; init; }
}
