using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record LoginDto
{
    [Required]
    [EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}
