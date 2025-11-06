using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<ApplicationUser> userManager,
                            ApplicationDbContext db,
                            ITokenService tokenService,
                            IConfiguration config) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterDto dto)
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
            : BadRequest(result.Errors);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user == null) return Unauthorized("Invalid email or password.");

        var valid = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!valid) return Unauthorized("Invalid email or password.");

        var accessToken = tokenService.GenerateAccessToken(user);

        var rawRefresh = tokenService.GenerateRefreshToken();
        var hashed = tokenService.HashToken(rawRefresh);

        // Save Refresh token in DB
        var rt = new RefreshToken
        {
            Token = hashed,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenDays"] ?? "7")),
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync();

        // Set Cookie
        Response.Cookies.Append("refreshToken", rawRefresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Domain = ".dev.local",
            Expires = rt.ExpiresAt
        });

        var expiresAt = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:AccessToken"] ?? "15"));
        return Ok(new AuthResponseDto { AccessToken = accessToken, ExpiresAt = expiresAt });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var token))
        {
            return Unauthorized();
        }

        var hashed = tokenService.HashToken(token);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == hashed);

        if (rt == null || rt.IsRevoked || rt.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized();
        }

        rt.IsRevoked = true;

        var newToken = tokenService.GenerateRefreshToken();
        var newHashToken = tokenService.HashToken(newToken);

        rt.ReplacedByToken = newToken;

        var newRt = new RefreshToken
        {
            Token = newToken,
            UserId = rt.UserId,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(config["Jwt:RefreshTokenDays"] ?? "7")),
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
        };

        db.RefreshTokens.Add(newRt);
        await db.SaveChangesAsync();

        var user = await userManager.FindByIdAsync(rt.UserId);
        if (user == null)
        {
            return Unauthorized();
        }

        var accessToken = tokenService.GenerateAccessToken(user);
        var exipresAt = DateTime.UtcNow.AddMinutes(int.Parse(config["Jwt:AccessTokenMinutes"] ?? "15"));
        return Ok(new AuthResponseDto { AccessToken = accessToken, ExpiresAt = exipresAt });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue("refreshToken", out var token))
        {
            var rt = await db.RefreshTokens.SingleOrDefaultAsync(r => r.Token == token);
            if (rt != null)
            {
                rt.IsRevoked = true;
                await db.SaveChangesAsync();
            }
        }

        Response.Cookies.Delete("refreshToken", new CookieOptions
        {
            Domain = ".dev.local",
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None
        });

        return Ok();
    }
}
