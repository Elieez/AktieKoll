using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IAuthService authService,
    IConfiguration config) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private CookieOptions BuildRefreshCookieOptions(DateTime expires) => new()
    {
        HttpOnly = true,
        Secure = config.GetValue<bool>("CookieSettings:Secure"),
        SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        Expires = expires
    };

    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
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
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);

        if (user == null)
            return Unauthorized("Invalid email or password.");

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized("Account is locked. Please try again later.");

        var valid = await userManager.CheckPasswordAsync(user, dto.Password);
        if (!valid)
            return Unauthorized("Invalid email or password.");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var pair = await authService.IssueTokenPairAsync(user, ipAddress);

        Response.Cookies.Append(RefreshTokenCookieName, pair.RawRefreshToken,
            BuildRefreshCookieOptions(pair.RefreshTokenExpiresAt));

        return Ok(new AuthResponseDto { AccessToken = pair.AccessToken, ExpiresAt = pair.AccessTokenExpiresAt });
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out var token))
        {
            return Unauthorized("No Refresh token");
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var pair = await authService.RotateTokenPairAsync(token, ipAddress);

        if (pair == null)
            return Unauthorized("Invalid or expired refresh token.");

        Response.Cookies.Append(RefreshTokenCookieName, pair.RawRefreshToken,
            BuildRefreshCookieOptions(pair.RefreshTokenExpiresAt));

        return Ok(new AuthResponseDto { AccessToken = pair.AccessToken, ExpiresAt = pair.AccessTokenExpiresAt });
    }

    [HttpPost("logout")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var token))
            await authService.RevokeRefreshTokenAsync(token);

        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = config.GetValue<bool>("CookieSettings:Secure"),
            SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        });
        return Ok();
    }
}
