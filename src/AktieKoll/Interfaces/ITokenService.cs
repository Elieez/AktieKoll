using AktieKoll.Models;

namespace AktieKoll.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user);
    string GenerateRefreshToken();
    string HashToken(string token);
}
