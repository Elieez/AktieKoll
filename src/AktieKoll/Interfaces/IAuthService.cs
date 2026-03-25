using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public record IssuedTokenPair(
    string AccessToken,
    string RawRefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);

public interface IAuthService
{
    Task<IssuedTokenPair> IssueTokenPairAsync(ApplicationUser user, string? ipAddress);
    Task<IssuedTokenPair?> RotateTokenPairAsync(string rawRefreshToken, string? ipAddress);
    Task RevokeRefreshTokenAsync(string rawRefreshToken);
}