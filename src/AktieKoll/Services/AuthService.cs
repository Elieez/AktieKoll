using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class AuthService(
    ApplicationDbContext db,
    ITokenService tokenService,
    UserManager<ApplicationUser> userManager,
    IConfiguration config) : IAuthService
{
    public async Task<IssuedTokenPair> IssueTokenPairAsync(ApplicationUser user, string? ipAddress)
    {
        var rawToken = tokenService.GenerateRefreshToken();
        var hashed = tokenService.HashToken(rawToken);
        var refreshDays   = config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var accessMinutes = config.GetValue<int>("Jwt:AccessTokenMinutes", 15);

        var rt = new RefreshToken
        {
            Token        = hashed,
            UserId       = user.Id,
            ExpiresAt    = DateTime.UtcNow.AddDays(refreshDays),
            CreatedByIp  = ipAddress
        };

        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        var accessToken = tokenService.GenerateAccessToken(user);
        return new IssuedTokenPair(
            accessToken,
            rawToken,
            DateTime.UtcNow.AddMinutes(accessMinutes),
            rt.ExpiresAt);
    }

    public async Task<IssuedTokenPair?> RotateTokenPairAsync(string rawRefreshToken, string? ipAddress)
    {
        var hashed = tokenService.HashToken(rawRefreshToken);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == hashed);

        if (rt == null || rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow)
            return null;

        var user = await userManager.FindByIdAsync(rt.UserId);
        if (user == null)
            return null;

        var refreshDays   = config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var accessMinutes = config.GetValue<int>("Jwt:AccessTokenMinutes", 15);

        var newRawToken = tokenService.GenerateRefreshToken();
        var newHashed   = tokenService.HashToken(newRawToken);

        rt.IsRevoked        = true;
        rt.ReplacedByToken  = newHashed;

        var newRt = new RefreshToken
        {
            Token       = newHashed,
            UserId      = rt.UserId,
            ExpiresAt   = DateTime.UtcNow.AddDays(refreshDays),
            CreatedByIp = ipAddress
        };

        db.RefreshTokens.Add(newRt);
        await db.SaveChangesAsync();

        var accessToken = tokenService.GenerateAccessToken(user);
        return new IssuedTokenPair(
            accessToken,
            newRawToken,
            DateTime.UtcNow.AddMinutes(accessMinutes),
            newRt.ExpiresAt);
    }

    public async Task RevokeRefreshTokenAsync(string rawRefreshToken)
    {
        var hashed = tokenService.HashToken(rawRefreshToken);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == hashed);
        if (rt == null) return;

        rt.IsRevoked = true;
        await db.SaveChangesAsync();
    }

    public async Task RevokeAllUserTokensAsync(string userId)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync();

        foreach (var t in tokens)
            t.IsRevoked = true;

        await db.SaveChangesAsync();
    }
}
