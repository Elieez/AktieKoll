using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record ResetPasswordDto
{
    [Required]
    [MaxLength(16)]
    public required string Code { get; init; }

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public required string NewPassword { get; init; }
}
