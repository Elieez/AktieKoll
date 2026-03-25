using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record ResetPasswordDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; init; }

    [Required]
    public required string Token { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}
