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

namespace AktieKoll.Tests.Integration.Controllers;

public class AuthControllerTests : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime, IDisposable
{
    private readonly WebApplicationFactoryFixture _factory;
    private readonly HttpClient _client;
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public AuthControllerTests(WebApplicationFactoryFixture factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public ValueTask InitializeAsync()
    {
        _factory.ResetDatabase();
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_ReturnsOkAndCreatesUser()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Password = "StrongPassword123!@#",
            DisplayName = "New User"
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // FIX: Use fresh scope from factory
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(registerDto.Email);
        user.Should().NotBeNull();
        user!.Email.Should().Be(registerDto.Email);
        user.DisplayName.Should().Be(registerDto.DisplayName);
    }

    [Fact]
    public async Task Register_WithExistingEmail_ReturnsBadRequest()
    {
        // Arrange
        // Create user using fresh scope
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = "existing@example.com",
                Email = "existing@example.com",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "StrongPassword123!@#");
        }

        var registerDto = new RegisterDto
        {
            Email = "existing@example.com",
            Password = "StrongPassword123!@#",
            DisplayName = "Duplicate User"
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Password = "weak",
            DisplayName = "New User"
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithoutDisplayName_CreatesUserSuccessfully()
    {
        // Arrange
        var registerDto = new RegisterDto
        {
            Email = "newuser@example.com",
            Password = "StrongPassword123!@#",
            DisplayName = null
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/register", registerDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(registerDto.Email);
        user.Should().NotBeNull();
        user!.DisplayName.Should().BeNull();
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndSetsRefreshTokenCookie()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        // Create user using fresh scope
        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                DisplayName = "Test User",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        var loginDto = new LoginDto
        {
            Email = email,
            Password = password
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await response.Content.ReadFromJsonTestAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();
        authResponse.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Verify refresh token cookie
        response.Headers.Should().ContainKey("Set-Cookie");
        var cookies = response.Headers.GetValues("Set-Cookie");
        cookies.Should().Contain(c => c.Contains("refreshToken"));
        cookies.Should().Contain(c => c.Contains("httponly", StringComparison.OrdinalIgnoreCase));

        // Verify refresh token in database using fresh scope
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(Token);
            refreshToken.Should().NotBeNull();
            refreshToken!.IsRevoked.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto
        {
            Email = "nonexistent@example.com",
            Password = "SomePassword123!@#"
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var email = "testuser@example.com";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, "CorrectPassword123!@#");
        }

        var loginDto = new LoginDto
        {
            Email = email,
            Password = "WrongPassword123!@#"
        };

        // Act
        var response = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CreatesRefreshTokenWithCorrectExpiration()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        var loginDto = new LoginDto { Email = email, Password = password };

        // Act
        await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);

        // Assert
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var refreshToken = await db.RefreshTokens.FirstOrDefaultAsync(Token);
            refreshToken.Should().NotBeNull();
            refreshToken!.ExpiresAt.Should().BeCloseTo(
                DateTime.UtcNow.AddDays(7),
                TimeSpan.FromMinutes(1));
        }
    }

    #endregion

    #region Refresh Tests

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewAccessTokenAndRevokesOldToken()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        // Login to get refresh token
        var loginDto = new LoginDto { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();

        var refreshTokenCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.Contains("refreshToken"));

        var clientWithCookie = _factory.CreateClient();
        AuthTestHelper.CreateClientWithCookie(clientWithCookie, refreshTokenCookie!);

        // Act
        var refreshResponse = await clientWithCookie.PostTestAsync("/api/auth/refresh");

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var authResponse = await refreshResponse.Content.ReadFromJsonTestAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrEmpty();

        // Check tokens using fresh scope
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Old token should be revoked
            var oldToken = await db.RefreshTokens
                .Where(rt => rt.IsRevoked)
                .FirstOrDefaultAsync(Token);
            oldToken.Should().NotBeNull();

            // New token should exist
            var newToken = await db.RefreshTokens
                .Where(rt => !rt.IsRevoked)
                .FirstOrDefaultAsync(Token);
            newToken.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostTestAsync("/api/auth/refresh");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_ReturnsUnauthorized()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        var loginDto = new LoginDto { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();

        // Revoke the token
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = await db.RefreshTokens.FirstAsync(Token);
            token.IsRevoked = true;
            await db.SaveChangesAsync(Token);
        }

        var refreshTokenCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.Contains("refreshToken"));

        var clientWithCookie = _factory.CreateClient();
        AuthTestHelper.CreateClientWithCookie(clientWithCookie, refreshTokenCookie!);

        // Act
        var refreshResponse = await clientWithCookie.PostTestAsync("/api/auth/refresh");

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        var loginDto = new LoginDto { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();

        // Expire the token
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = await db.RefreshTokens.FirstAsync(Token);
            token.ExpiresAt = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync(Token);
        }

        var refreshTokenCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.Contains("refreshToken"));

        var clientWithCookie = _factory.CreateClient();
        AuthTestHelper.CreateClientWithCookie(clientWithCookie, refreshTokenCookie!);

        // Act
        var refreshResponse = await clientWithCookie.PostTestAsync("/api/auth/refresh");

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WithValidToken_RevokesRefreshTokenAndDeletesCookie()
    {
        // Arrange
        var email = "testuser@example.com";
        var password = "TestPassword123!@#";

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(user, password);
        }

        var loginDto = new LoginDto { Email = email, Password = password };
        var loginResponse = await _client.PostAsJsonTestAsync("/api/auth/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();

        var refreshTokenCookie = loginResponse.Headers.GetValues("Set-Cookie")
            .FirstOrDefault(c => c.Contains("refreshToken"));

        var clientWithCookie = _factory.CreateClient();
        AuthTestHelper.CreateClientWithCookie(clientWithCookie, refreshTokenCookie!);

        // Act
        var logoutResponse = await clientWithCookie.PostTestAsync("/api/auth/logout");

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Token should be revoked
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var token = await db.RefreshTokens.FirstOrDefaultAsync(Token);
            token.Should().NotBeNull();
            token!.IsRevoked.Should().BeTrue();
        }

        // Cookie should be deleted (expires header present)
        var logoutCookies = logoutResponse.Headers.GetValues("Set-Cookie");
        logoutCookies.Should().Contain(c => c.Contains("refreshToken") && c.Contains("expires"));
    }

    [Fact]
    public async Task Logout_WithoutCookie_ReturnsOkWithoutError()
    {
        // Act
        var response = await _client.PostTestAsync("/api/auth/logout");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
