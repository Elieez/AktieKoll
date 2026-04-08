using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    IAuthService authService,
    IEmailService emailService,
    IConfiguration config,
    ApplicationDbContext db) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";

    private CookieOptions BuildRefreshCookieOptions(DateTime expires) => new()
    {
        HttpOnly  = true,
        Secure    = config.GetValue<bool>("CookieSettings:Secure"),
        SameSite  = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        Expires   = expires
    };


    /// <summary>Register a new user. Sends a verification email on success.</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var user = new ApplicationUser
        {
            UserName    = dto.Email,
            Email       = dto.Email,
            DisplayName = dto.DisplayName,
        };

        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description).ToList() });

        // Send verification email (fire-and-forget; don't fail registration if email fails)
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var code = GenerateShortCode();
            db.VerificationCodes.Add(new VerificationCode
            {
                Code      = code,
                UserId    = user.Id,
                Token     = token,
                Purpose   = "email_confirmation",
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            });
            await db.SaveChangesAsync();
            await emailService.SendEmailVerificationAsync(user.Email!, code);
        }
        catch { /* log but don't surface */ }

        return Ok(new { message = "User created successfully. Check your email to verify your account." });
    }


    /// <summary>Authenticate with email and password. Returns JWT access token and sets refresh-token cookie.</summary>
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

    /// <summary>Rotate the refresh token and return a new JWT access token.</summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(RefreshTokenCookieName, out var token))
            return Unauthorized("No refresh token.");

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var pair = await authService.RotateTokenPairAsync(token, ipAddress);

        if (pair == null)
            return Unauthorized("Invalid or expired refresh token.");

        Response.Cookies.Append(RefreshTokenCookieName, pair.RawRefreshToken,
            BuildRefreshCookieOptions(pair.RefreshTokenExpiresAt));

        return Ok(new AuthResponseDto { AccessToken = pair.AccessToken, ExpiresAt = pair.AccessTokenExpiresAt });
    }

    /// <summary>Revoke the current refresh token and clear the cookie.</summary>
    [HttpPost("logout")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(RefreshTokenCookieName, out var token))
            await authService.RevokeRefreshTokenAsync(token);

        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure   = config.GetValue<bool>("CookieSettings:Secure"),
            SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        });

        return Ok();
    }

    /// <summary>Initiate Google OAuth — redirects the browser to Google's consent screen.</summary>
    [HttpGet("google")]
    public IActionResult GoogleLogin()
    {
        var frontendUrl  = (config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/');
        var redirectUri  = $"{Request.Scheme}://{Request.Host}/api/auth/google/handle";
        var props        = new AuthenticationProperties
        {
            RedirectUri = redirectUri,
            Items       = { ["returnUrl"] = $"{frontendUrl}/auth/callback" }
        };
        return Challenge(props, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Google OAuth callback handler. Called by the Google middleware after /api/auth/google/callback
    /// is processed. Creates or links the account then redirects to the frontend with tokens.
    /// </summary>
    [HttpGet("google/handle")]
    public async Task<IActionResult> GoogleHandle()
    {
        var result = await HttpContext.AuthenticateAsync(IdentityConstants.ExternalScheme);
        if (!result.Succeeded)
            return Redirect(BuildFrontendErrorUrl("google_auth_failed"));

        var principal  = result.Principal;
        var googleId   = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email      = principal.FindFirstValue(ClaimTypes.Email);
        var name       = principal.FindFirstValue(ClaimTypes.Name);
        var avatarUrl  = principal.FindFirstValue("picture") ?? string.Empty;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(googleId))
            return Redirect(BuildFrontendErrorUrl("google_missing_claims"));

        // Find existing user by Google login or by email
        var user = await userManager.FindByLoginAsync("Google", googleId);
        if (user == null)
        {
            user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // New user — create account (Google-verified, no password)
                user = new ApplicationUser
                {
                    UserName           = email,
                    Email              = email,
                    EmailConfirmed     = true,   // Google already verified this
                    DisplayName        = name,
                    GoogleId           = googleId,
                    GoogleAvatarUrl    = avatarUrl,
                    GoogleDisplayName  = name,
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return Redirect(BuildFrontendErrorUrl("create_failed"));
            }
            else
            {
                // Existing email account — link Google to it
                user.GoogleId          = googleId;
                user.GoogleAvatarUrl   = avatarUrl;
                user.GoogleDisplayName = name;
                user.EmailConfirmed    = true;   // confirm email via Google
                await userManager.UpdateAsync(user);
            }

            await userManager.AddLoginAsync(user, new UserLoginInfo("Google", googleId, "Google"));
        }
        else
        {
            // Update avatar in case it changed
            user.GoogleAvatarUrl   = avatarUrl;
            user.GoogleDisplayName = name;
            await userManager.UpdateAsync(user);
        }

        // Sign out the temporary external cookie
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var pair      = await authService.IssueTokenPairAsync(user, ipAddress);

        // Set refresh token cookie
        Response.Cookies.Append(RefreshTokenCookieName, pair.RawRefreshToken,
            BuildRefreshCookieOptions(pair.RefreshTokenExpiresAt));

        // Redirect frontend — access token in URL (short-lived 15 min; frontend removes it immediately)
        var frontendCallback = result.Properties?.Items.TryGetValue("returnUrl", out var ret) == true
            ? ret ?? $"{(config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/')}/auth/callback"
            : $"{(config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/')}/auth/callback";

        return Redirect($"{frontendCallback}?token={Uri.EscapeDataString(pair.AccessToken)}");
    }

    /// <summary>Verify email address using the short code sent to the user's inbox.</summary>
    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest(new { error = "Ogiltig verifieringslänk." });

        var record = await db.VerificationCodes
            .FirstOrDefaultAsync(v => v.Code == code && v.Purpose == "email_confirmation");

        if (record == null || record.Used || record.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Länken är ogiltig eller har löpt ut." });

        var user = await userManager.FindByIdAsync(record.UserId);
        if (user == null)
            return BadRequest(new { error = "Ogiltig verifieringslänk." });

        var result = await userManager.ConfirmEmailAsync(user, record.Token);
        if (!result.Succeeded)
            return BadRequest(new { error = "Länken är ogiltig eller har löpt ut." });

        record.Used = true;
        await db.SaveChangesAsync();

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var pair = await authService.IssueTokenPairAsync(user, ipAddress);

        Response.Cookies.Append(RefreshTokenCookieName, pair.RawRefreshToken,
            BuildRefreshCookieOptions(pair.RefreshTokenExpiresAt));

        return Ok(new AuthResponseDto { AccessToken = pair.AccessToken, ExpiresAt = pair.AccessTokenExpiresAt });
    }

    /// <summary>Resend email verification link. Requires the user to be authenticated.</summary>
    [HttpPost("resend-verification")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await userManager.FindByIdAsync(userId!);
        if (user == null) return Unauthorized();

        if (user.EmailConfirmed)
            return BadRequest(new { error = "E-postadressen är redan verifierad." });

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = GenerateShortCode();
        db.VerificationCodes.Add(new VerificationCode
        {
            Code      = code,
            UserId    = user.Id,
            Token     = token,
            Purpose   = "email_confirmation",
            ExpiresAt = DateTime.UtcNow.AddHours(24)
        });
        await db.SaveChangesAsync();
        await emailService.SendEmailVerificationAsync(user.Email!, code);

        return Ok(new { message = "Verifieringsmejl skickat." });
    }

    /// <summary>
    /// Request a password reset email. Always returns 200 to prevent email enumeration.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user != null)
        {
            try
            {
                var token = await userManager.GeneratePasswordResetTokenAsync(user);
                var code = GenerateShortCode();
                db.VerificationCodes.Add(new VerificationCode
                {
                    Code      = code,
                    UserId    = user.Id,
                    Token     = token,
                    Purpose   = "password_reset",
                    ExpiresAt = DateTime.UtcNow.AddHours(1)
                });
                await db.SaveChangesAsync();
                await emailService.SendPasswordResetAsync(user.Email!, code);
            }
            catch { /* log but never reveal */ }
        }

        // Always 200 — prevents email enumeration
        return Ok(new { message = "Om kontot finns skickas ett återställningsmejl inom kort." });
    }

    /// <summary>Reset the user's password using the short code from the reset email.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var record = await db.VerificationCodes
            .FirstOrDefaultAsync(v => v.Code == dto.Code && v.Purpose == "password_reset");

        if (record == null || record.Used || record.ExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Länken är ogiltig eller har löpt ut." });

        var user = await userManager.FindByIdAsync(record.UserId);
        if (user == null)
            return BadRequest(new { error = "Ogiltig återställningslänk." });

        var result = await userManager.ResetPasswordAsync(user, record.Token, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            return BadRequest(new { error = "Länken är ogiltig eller har löpt ut.", details = errors });
        }

        record.Used = true;
        await db.SaveChangesAsync();

        // Invalidate all existing refresh tokens for security
        await authService.RevokeAllUserTokensAsync(user.Id);

        return Ok(new { message = "Lösenordet har ändrats. Du kan nu logga in." });
    }

    /// <summary>
    /// Step 1 of account deletion: sends a confirmation email with a 1-hour deletion token.
    /// Does NOT delete anything yet.
    /// </summary>
    [HttpPost("account/delete/request")]
    [Authorize]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> RequestAccountDeletion()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await userManager.FindByIdAsync(userId!);
        if (user == null) return Unauthorized();

        // Generate a secure raw token and store its hash + expiry on the user row
        var rawToken  = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = HashDeletionToken(rawToken);

        user.DeletionTokenHash      = tokenHash;
        user.DeletionTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        await userManager.UpdateAsync(user);

        await emailService.SendAccountDeletionRequestAsync(user.Email!, rawToken);

        return Ok(new { message = "Bekräftelsemejl skickat. Kontrollera din inkorg." });
    }

    /// <summary>
    /// Step 2 of account deletion: validates the token from the email plus the user's password,
    /// then permanently hard-deletes all user data.
    /// </summary>
    [HttpPost("account/delete/confirm")]
    [Authorize]
    [EnableRateLimiting("sensitive")]
    public async Task<IActionResult> ConfirmAccountDeletion([FromBody] AccountDeleteConfirmDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user   = await userManager.FindByIdAsync(userId!);
        if (user == null) return Unauthorized();

        // Validate deletion token
        if (string.IsNullOrEmpty(user.DeletionTokenHash) ||
            user.DeletionTokenExpiresAt == null ||
            user.DeletionTokenExpiresAt < DateTime.UtcNow)
            return BadRequest(new { error = "Länken har löpt ut. Begär en ny borttagningslänk." });

        var suppliedHash = HashDeletionToken(dto.Token);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(suppliedHash),
                Encoding.UTF8.GetBytes(user.DeletionTokenHash)))
            return BadRequest(new { error = "Ogiltig bekräftelsekod." });

        // Password check (skip for Google-only accounts that have no password)
        var hasPassword = await userManager.HasPasswordAsync(user);
        if (hasPassword)
        {
            if (string.IsNullOrEmpty(dto.Password))
                return BadRequest(new { error = "Lösenord krävs för att bekräfta borttagningen." });

            var passwordOk = await userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordOk)
                return BadRequest(new { error = "Felaktigt lösenord." });
        }

        var email = user.Email!;

        // Revoke all tokens
        await authService.RevokeAllUserTokensAsync(user.Id);

        // Hard delete
        var deleteResult = await userManager.DeleteAsync(user);
        if (!deleteResult.Succeeded)
            return StatusCode(500, new { error = "Kontot kunde inte raderas. Försök igen senare." });

        // Clear auth cookie
        Response.Cookies.Delete(RefreshTokenCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure   = config.GetValue<bool>("CookieSettings:Secure"),
            SameSite = config.GetValue<SameSiteMode>("CookieSettings:SameSite", SameSiteMode.Lax),
        });

        // Send confirmation email (fire-and-forget)
        try { await emailService.SendAccountDeletedConfirmationAsync(email); } catch { }

        return Ok(new { message = "Ditt konto och all tillhörande data har raderats permanent." });
    }

    // ─────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────

    private static string HashDeletionToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }

    private static string GenerateShortCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return RandomNumberGenerator.GetString(chars, 8);
    }

    private string BuildFrontendErrorUrl(string code)
    {
        var frontendUrl = (config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/');
        return $"{frontendUrl}/auth?error={code}";
    }
}
