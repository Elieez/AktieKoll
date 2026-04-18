using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Dtos;

public record AuthResponseDto
{
    [Required]
    public required string AccessToken { get; init; }

    [Required]
    public required DateTime ExpiresAt { get; init; }
}
