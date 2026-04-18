using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record AccountDeleteConfirmDto
{
    [Required]
    public required string Token { get; init; }

    /// <summary>Required for password-based accounts; optional for Google-only accounts.</summary>
    public string? Password { get; init; }
}
