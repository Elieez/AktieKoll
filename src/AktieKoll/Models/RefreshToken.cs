namespace AktieKoll.Models;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Token { get; set; }
    public required string UserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
    public string? ReplacedByToken { get; set; }
}
