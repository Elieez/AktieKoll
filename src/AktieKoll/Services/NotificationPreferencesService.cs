using AktieKoll.Data;
using AktieKoll.Dtos;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class NotificationPreferencesService(ApplicationDbContext context) : INotificationPreferencesService
{
    public async Task<NotificationPreferencesDto> GetAsync(string userId, CancellationToken ct = default)
    {
        var pref = await context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            return new NotificationPreferencesDto
            {
                EmailEnabled = true,
                DiscordEnabled = false,
                DiscordWebhookUrl = null
            };
        }

        return new NotificationPreferencesDto
        {
            EmailEnabled = pref.EmailEnabled,
            DiscordEnabled = pref.DiscordEnabled,
            DiscordWebhookUrl = pref.DiscordWebhookUrl
        };
    }

    public async Task<ServiceResult<NotificationPreferencesDto>> UpdateAsync(
        string userId, UpdateNotificationPreferencesDto dto, CancellationToken ct = default)
    {
        if (dto.DiscordEnabled && !string.IsNullOrEmpty(dto.DiscordWebhookUrl))
        {
            if (!Uri.TryCreate(dto.DiscordWebhookUrl, UriKind.Absolute, out var uri) ||
                !uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<NotificationPreferencesDto>.Fail(
                    "Ogiltig Discord-webhook-URL. Måste vara från discord.com.", 400);
            }
        }

        var pref = await context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (pref is null)
        {
            pref = new NotificationPreference
            {
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            context.NotificationPreferences.Add(pref);
        }

        pref.EmailEnabled = dto.EmailEnabled;
        pref.DiscordEnabled = dto.DiscordEnabled;
        pref.DiscordWebhookUrl = dto.DiscordEnabled ? dto.DiscordWebhookUrl : null;
        pref.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        return ServiceResult<NotificationPreferencesDto>.Ok(new NotificationPreferencesDto
        {
            EmailEnabled = pref.EmailEnabled,
            DiscordEnabled = pref.DiscordEnabled,
            DiscordWebhookUrl = pref.DiscordWebhookUrl
        });
    }
}