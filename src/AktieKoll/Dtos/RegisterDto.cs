namespace AktieKoll.Dtos;

public record RegisterDto
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public string? DisplayName { get; init; }
}
