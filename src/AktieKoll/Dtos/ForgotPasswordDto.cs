using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public required string Email { get; init; }
}
