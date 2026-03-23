using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db,
    ITokenService tokenService,
    IConfiguration config) : ControllerBase
{

    private CookieOptions BuildRefreshCookieOptions(DateTime expires) => new()
    {
        HttpOnly = true,
        Secure = config.GetValue<bool>("CookieSettings:Secure"),
        SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        Expires = expires
    };

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody]RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = dto.DisplayName,
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        return result.Succeeded
            ? Ok(new { message = "User created successfully." })
            : BadRequest(result.Errors.Select(e => e.Description));
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody]LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        if (user == null) 
            return Unauthorized("Invalid email or password.");

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized("Account is locked. Please try again later.");

        var valid = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!valid) 
            return Unauthorized("Invalid email or password.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var rawRefresh = tokenService.GenerateRefreshToken();
        var hashed = tokenService.HashToken(rawRefresh);

        var refreshTokenDays = config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var accessTokenMinutes = config.GetValue<int>("Jwt:AccessTokenMinutes", 15);

        // Save Refresh token in DB
        var rt = new RefreshToken
        {
            Token = hashed,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };
        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        // Set Cookie
        Response.Cookies.Append("refreshToken", rawRefresh, BuildRefreshCookieOptions(rt.ExpiresAt));

        var expiresAt = DateTime.UtcNow.AddMinutes(accessTokenMinutes);

        return Ok(new AuthResponseDto { AccessToken = accessToken, ExpiresAt = expiresAt });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("api")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var token))
        {
            return Unauthorized("No Refresh token");
        }

        var hashed = tokenService.HashToken(token);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == hashed);

        if (rt == null || rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized("Invalid or expired refresh token");
        }

        var user = await userManager.FindByIdAsync(rt.UserId);
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        rt.IsRevoked = true;

        var newRawToken = tokenService.GenerateRefreshToken();
        var newHashToken = tokenService.HashToken(newRawToken);

        rt.ReplacedByToken = newHashToken;

        var refreshTokenDays = config.GetValue<int>("Jwt:RefreshTokenDays", 7);
        var accessTokenMinutes = config.GetValue<int>("Jwt:AccessTokenMinutes", 15);

        var newRt = new RefreshToken
        {
            Token = newHashToken,
            UserId = rt.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        db.RefreshTokens.Add(newRt);
        await db.SaveChangesAsync();

        var accessToken = tokenService.GenerateAccessToken(user);
        var expiresAt = DateTime.UtcNow.AddMinutes(accessTokenMinutes);

        Response.Cookies.Append("refreshToken", newRawToken, BuildRefreshCookieOptions(newRt.ExpiresAt));

        return Ok(new AuthResponseDto { AccessToken = accessToken, ExpiresAt = expiresAt });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("refreshToken", out var token))
        {
            var hashed = tokenService.HashToken(token);

            var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == hashed);
            if (rt != null)
            {
                rt.IsRevoked = true;
                await db.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            HttpOnly = true,
            Secure = config.GetValue<bool>("CookieSettings:Secure"),
            SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        });

        return Ok();
    }
}
