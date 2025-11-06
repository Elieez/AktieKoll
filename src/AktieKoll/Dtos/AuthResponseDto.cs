namespace AktieKoll.Dtos;

public record AuthResponseDto
{
    public required string AccessToken { get; init; }
    public required DateTime ExpiresAt { get; init; }
}
