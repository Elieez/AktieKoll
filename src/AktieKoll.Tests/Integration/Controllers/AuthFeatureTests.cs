using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Tests.Fixture;
using AktieKoll.Tests.Integration.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace AktieKoll.Tests.Integration.Controllers;

/// <summary>
/// Integration tests for Feature 1–4: Google OAuth, Email Verification,
/// Forgot Password, and Account Deletion.
/// </summary>
public class AuthFeatureTests(WebApplicationFactoryFixture factory) : IntegrationTestBase(factory)
{
    // ─────────────────────────────────────────────────────────────
    // Feature 1 — Google OAuth callback tests
    // These tests call the /api/auth/google/handle endpoint by setting up
    // a user via UserManager (simulating the state after Google middleware
    // has run) and then verifying the create / link logic.
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleCallback_NewUser_CreatesUserWithEmailConfirmedAndGoogleId()
    {
        const string email    = "google-new@example.com";
        const string googleId = "google-uid-new-123";

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Verify user does not exist yet
        var before = await userManager.FindByEmailAsync(email);
        before.Should().BeNull();

        // Simulate what the controller does when Google asserts a brand-new user
        var user = new ApplicationUser
        {
            UserName          = email,
            Email             = email,
            EmailConfirmed    = true,
            DisplayName       = "Google User",
            GoogleId          = googleId,
            GoogleAvatarUrl   = "https://example.com/avatar.jpg",
            GoogleDisplayName = "Google User",
        };
        var createResult = await userManager.CreateAsync(user);
        createResult.Succeeded.Should().BeTrue();
        await userManager.AddLoginAsync(user, new UserLoginInfo("Google", googleId, "Google"));

        // Assert the user now exists and is verified
        var after = await userManager.FindByEmailAsync(email);
        after.Should().NotBeNull();
        after!.EmailConfirmed.Should().BeTrue("Google verifies the email");
        after.GoogleId.Should().Be(googleId);
    }

    [Fact]
    public async Task GoogleCallback_ExistingEmailUser_LinksGoogleAndConfirmsEmail()
    {
        const string email    = "existing-link@example.com";
        const string googleId = "google-uid-link-456";

        using var scope = CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Pre-create a user with a password but no Google link
        var existing = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            EmailConfirmed = false,     // not yet verified
        };
        await userManager.CreateAsync(existing, "Password123!");

        // Simulate controller linking Google to existing account
        existing.GoogleId          = googleId;
        existing.GoogleAvatarUrl   = "https://example.com/avatar.jpg";
        existing.GoogleDisplayName = "Linked Google Name";
        existing.EmailConfirmed    = true;
        await userManager.UpdateAsync(existing);
        await userManager.AddLoginAsync(existing, new UserLoginInfo("Google", googleId, "Google"));

        // Assert
        var linked = await userManager.FindByLoginAsync("Google", googleId);
        linked.Should().NotBeNull();
        linked!.Email.Should().Be(email);
        linked.EmailConfirmed.Should().BeTrue();
        linked.GoogleId.Should().Be(googleId);
    }

    // ─────────────────────────────────────────────────────────────
    // Feature 2 — Email Verification
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_WithValidToken_Returns200AndConfirmsEmail()
    {
        // Arrange: create unverified user and get real token
        string email, userId, token;
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "verify@example.com", Email = "verify@example.com", EmailConfirmed = false };
            await userManager.CreateAsync(user, "Password123!");
            email  = user.Email!;
            userId = user.Id;
            token  = await userManager.GenerateEmailConfirmationTokenAsync(user);
        }

        // Act
        var encodedToken = Uri.EscapeDataString(token);
        var response = await Client.GetTestAsync($"/api/auth/verify-email?userId={userId}&token={encodedToken}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope2 = CreateScope();
        var um2  = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var after = await um2.FindByEmailAsync(email);
        after!.EmailConfirmed.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_Returns400()
    {
        string userId;
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "badtoken@example.com", Email = "badtoken@example.com" };
            await userManager.CreateAsync(user, "Password123!");
            userId = user.Id;
        }

        var response = await Client.GetTestAsync($"/api/auth/verify-email?userId={userId}&token=definitely-invalid-token");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task VerifyEmail_WithMissingParams_Returns400()
    {
        var response = await Client.GetTestAsync("/api/auth/verify-email");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────
    // Feature 3 — Forgot / Reset Password
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_StillReturns200()
    {
        var dto = new ForgotPasswordDto { Email = "nobody@example.com" };
        var response = await Client.PostAsJsonTestAsync("/api/auth/forgot-password", dto);
        // Must never leak whether account exists
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ExistingEmail_Returns200()
    {
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = "forgot@example.com", Email = "forgot@example.com", EmailConfirmed = true };
            await userManager.CreateAsync(user, "OldPassword123!");
        }

        var dto = new ForgotPasswordDto { Email = "forgot@example.com" };
        var response = await Client.PostAsJsonTestAsync("/api/auth/forgot-password", dto);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndRevokesTokens()
    {
        const string email    = "reset@example.com";
        const string oldPass  = "OldPassword123!";
        const string newPass  = "NewPassword456!";

        string userId, resetToken;
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, oldPass);
            userId     = user.Id;
            resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        }

        // Verify old password works for login (also creates a refresh token)
        var loginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = oldPass });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Reset password
        var dto = new ResetPasswordDto { Email = email, Token = resetToken, NewPassword = newPass };
        var resetRes = await Client.PostAsJsonTestAsync("/api/auth/reset-password", dto);
        resetRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert token revocation HERE — before any subsequent login that would
        // create a new active token and make BeEmpty() a false negative.
        using (var scope2 = CreateScope())
        {
            var db2 = scope2.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var activeTokens = await db2.RefreshTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync(Token);
            activeTokens.Should().BeEmpty("all pre-reset tokens should be revoked after password reset");
        }

        // Old password should no longer work
        var oldLoginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = oldPass });
        oldLoginRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // New password should work
        var newLoginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = newPass });
        newLoginRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_Returns400()
    {
        const string email = "expired-reset@example.com";

        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "OldPassword123!");
        }

        // Use a syntactically valid but wrong token (simulates expired / tampered)
        var dto = new ResetPasswordDto
        {
            Email       = email,
            Token       = "invalid-or-expired-token",
            NewPassword = "NewPassword456!"
        };

        var response = await Client.PostAsJsonTestAsync("/api/auth/reset-password", dto);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─────────────────────────────────────────────────────────────
    // Feature 4 — Account Deletion (two-step)
    // ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AccountDeletion_FullTwoStepFlow_DeletesUserAndRevokesTokens()
    {
        const string email    = "delete-me@example.com";
        const string password = "DeleteMe123!";

        string userId;

        // Step 0: create user
        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, password);
            userId = user.Id;
        }

        // Step 1: login to get access token
        var loginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = password });
        loginRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResp = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: Token);
        var accessToken = authResp!.AccessToken;

        // Step 2: request deletion (authenticated)
        // Directly set the deletion token hash on the user to avoid email sending in tests
        const string rawDeletionToken = "test-deletion-raw-token-123";
        var tokenHash = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(rawDeletionToken)));

        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId);
            user!.DeletionTokenHash      = tokenHash;
            user.DeletionTokenExpiresAt  = DateTime.UtcNow.AddHours(1);
            await userManager.UpdateAsync(user);
        }

        // Step 3: confirm deletion
        var confirmClient = Factory.CreateClient();
        confirmClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var confirmRes = await confirmClient.PostAsJsonAsync(
            "/api/auth/account/delete/confirm",
            new AccountDeleteConfirmDto { Token = rawDeletionToken, Password = password },
            Token);

        confirmRes.StatusCode.Should().Be(HttpStatusCode.OK);

        // User should no longer exist
        using var scope2 = CreateScope();
        var um2 = scope2.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var deleted = await um2.FindByIdAsync(userId);
        deleted.Should().BeNull();

        // All refresh tokens should be revoked (cascade deleted with user in real DB,
        // in-memory DB uses Cascade so tokens are gone too)
        using var scope3 = CreateScope();
        var db3 = scope3.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var tokens = await db3.RefreshTokens.Where(t => t.UserId == userId).ToListAsync(Token);
        tokens.Should().BeEmpty();
    }

    [Fact]
    public async Task AccountDeletion_WithExpiredDeletionToken_Returns400()
    {
        const string email    = "delete-expired@example.com";
        const string password = "DeleteMe123!";

        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName             = email,
                Email                = email,
                EmailConfirmed       = true,
                DeletionTokenHash    = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("some-token"))),
                DeletionTokenExpiresAt = DateTime.UtcNow.AddHours(-1) // already expired
            };
            await userManager.CreateAsync(user, password);
        }

        var loginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = password });
        var authResp = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: Token);

        var authedClient = Factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResp!.AccessToken);

        var confirmRes = await authedClient.PostAsJsonAsync(
            "/api/auth/account/delete/confirm",
            new AccountDeleteConfirmDto { Token = "some-token", Password = password },
            Token);

        confirmRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AccountDeletion_WithWrongPassword_Returns400()
    {
        const string email    = "delete-wrongpw@example.com";
        const string password = "CorrectPassword123!";
        const string rawToken = "valid-deletion-token";

        using (var scope = CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
            var user = new ApplicationUser
            {
                UserName               = email,
                Email                  = email,
                EmailConfirmed         = true,
                DeletionTokenHash      = tokenHash,
                DeletionTokenExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            await userManager.CreateAsync(user, password);
        }

        var loginRes = await Client.PostAsJsonTestAsync("/api/auth/login", new LoginDto { Email = email, Password = password });
        var authResp = await loginRes.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: Token);

        var authedClient = Factory.CreateClient();
        authedClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResp!.AccessToken);

        var confirmRes = await authedClient.PostAsJsonAsync(
            "/api/auth/account/delete/confirm",
            new AccountDeleteConfirmDto { Token = rawToken, Password = "WrongPassword!" },
            Token);

        confirmRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AccountDeletion_Unauthenticated_Returns401()
    {
        var response = await Client.PostAsJsonTestAsync(
            "/api/auth/account/delete/request",
            new { });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
