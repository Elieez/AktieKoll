using AktieKoll.Dtos;
using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;

namespace AktieKoll.Tests.Unit;

public class NotificationPreferencesServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    [Fact]
    public async Task Get_NoRow_ReturnsDefaults()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = new NotificationPreferencesService(ctx);

        var result = await service.GetAsync("user1", Ct);

        Assert.True(result.EmailEnabled);
        Assert.False(result.DiscordEnabled);
        Assert.Null(result.DiscordWebhookUrl);
    }

    [Fact]
    public async Task Get_ExistingRow_ReturnsSavedPrefs()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = "user1",
            EmailEnabled = false,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc",
            CreatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync(Ct);
        var service = new NotificationPreferencesService(ctx);

        var result = await service.GetAsync("user1", Ct);

        Assert.False(result.EmailEnabled);
        Assert.True(result.DiscordEnabled);
        Assert.Equal("https://discord.com/api/webhooks/123/abc", result.DiscordWebhookUrl);
    }

    [Fact]
    public async Task Update_ValidDiscordUrl_Saves()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = new NotificationPreferencesService(ctx);
        var dto = new UpdateNotificationPreferencesDto
        {
            EmailEnabled = true,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://discord.com/api/webhooks/123/abc"
        };

        var result = await service.UpdateAsync("user1", dto, Ct);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.DiscordEnabled);
        Assert.Equal("https://discord.com/api/webhooks/123/abc", result.Value.DiscordWebhookUrl);

        // Verify persisted
        var saved = ctx.NotificationPreferences.Single(p => p.UserId == "user1");
        Assert.True(saved.DiscordEnabled);
    }

    [Fact]
    public async Task Update_InvalidDiscordUrl_Returns400()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var service = new NotificationPreferencesService(ctx);
        var dto = new UpdateNotificationPreferencesDto
        {
            EmailEnabled = true,
            DiscordEnabled = true,
            DiscordWebhookUrl = "https://evil.com/webhook"
        };

        var result = await service.UpdateAsync("user1", dto, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }
}
