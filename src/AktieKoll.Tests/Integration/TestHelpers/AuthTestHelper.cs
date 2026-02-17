using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;

namespace AktieKoll.Tests.Integration.TestHelpers;

public class AuthTestHelper(IServiceProvider serviceProvider) : IDisposable
{
    private readonly IServiceScope _scope = serviceProvider.CreateScope();

    public ApplicationDbContext DbContext =>
        _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    public UserManager<ApplicationUser> UserManager =>
        _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    public async Task<ApplicationUser> CreateTestUserAsync(
        string email = "test@example.com",
        string password = "Test123!@#",
        string? displayName = "Test User"
        )
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = true
        };

        var result = await UserManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    public async Task<string> GetAccessTokenAsync(
        HttpClient client,
        string email = "test@example.com",
        string password = "Test123!@#",
        CancellationToken cancellationToken = default)
    {
        var existingUser = await UserManager.FindByEmailAsync(email);
        if (existingUser == null)
        {
            await CreateTestUserAsync(email, password);
        }

        var loginDto = new LoginDto { Email = email, Password = password };
        var response = await client.PostAsJsonAsync("/api/auth/login", loginDto, cancellationToken);
        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponseDto>(cancellationToken: cancellationToken);
        return authResponse!.AccessToken;
    }

    public static async Task<(HttpResponseMessage response, string refreshTokenCookie)> LoginUserAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var loginDto = new LoginDto
        {
            Email = email,
            Password = password
        };

        var response = await client.PostAsJsonAsync("/api/auth/login", loginDto, cancellationToken);

        string? refreshTokenCookie = null;
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            refreshTokenCookie = cookies.FirstOrDefault(c => c.Contains("refreshToken"));
        }

        return (response, refreshTokenCookie ?? string.Empty);
    }

    public static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        if (string.IsNullOrEmpty(setCookieHeader) || !setCookieHeader.StartsWith($"{cookieName}="))
        {
            return string.Empty;
        }

        var parts = setCookieHeader.Split(';')[0].Split('=');
        return parts.Length == 2 ? parts[1] : string.Empty;
    }

    public static HttpClient CreateClientWithCookie(HttpClient baseClient, string cookieHeader)
    {
        if (!string.IsNullOrEmpty(cookieHeader))
        {
            var cookieValue = cookieHeader.Split(';')[0];
            baseClient.DefaultRequestHeaders.Add("Cookie", cookieValue);
        }

        return baseClient;
    }

    public void Dispose()
    {
        _scope?.Dispose();
        GC.SuppressFinalize(this);
    }
}

// Extension methods for cleaner test syntax
public static class HttpClientTestExtensions
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostAsJsonTestAsync<T>(
        this HttpClient client,
        string requestUri,
        T value)
    {
        return client.PostAsJsonAsync(requestUri, value, Token);
    }

    public static Task<HttpResponseMessage> PostTestAsync(
        this HttpClient client,
        string requestUri,
        HttpContent? content = null)
    {
        return client.PostAsync(requestUri, content, Token);
    }

    public static Task<HttpResponseMessage> GetTestAsync(
        this HttpClient client,
        string requestUri)
        => client.GetAsync(requestUri, Token);

    public static Task<HttpResponseMessage> DeleteTestAsync(
        this HttpClient client,
        string requestUri)
        => client.DeleteAsync(requestUri, Token);

    public static Task<HttpResponseMessage> PutAsJsonTestAsync<T>(
        this HttpClient client,
        string requestUri,
        T value)
        => client.PutAsJsonAsync(requestUri, value, Token);
    
    public static Task<HttpResponseMessage> PatchAsJsonTestAsync<T>(
        this HttpClient client,
        string requestUri,
        T value)
        => client.PatchAsJsonAsync(requestUri, value, Token);

    public static Task<T?> ReadFromJsonTestAsync<T>(this HttpContent content)
    {
        return content.ReadFromJsonAsync<T>(cancellationToken: Token);
    }
}
