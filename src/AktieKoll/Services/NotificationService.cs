using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class NotificationService(
    ApplicationDbContext context,
    IEmailService emailService,
    IDiscordService discordService,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task ProcessBatchNotificationsAsync(
        string batchRunId,
        IEnumerable<InsiderTrade> newTrades,
        CancellationToken ct = default)
    {
        var tradeList = newTrades.ToList();
        if (tradeList.Count == 0)
        {
            logger.LogInformation("No new trades for batch {BatchRunId}, skipping notifications", batchRunId);
            return;
        }

        // Group by Symbol; skip trades without a resolved symbol
        var bySymbol = tradeList
            .Where(t => !string.IsNullOrEmpty(t.Symbol))
            .GroupBy(t => t.Symbol!)
            .ToDictionary(g => g.Key, g => g.ToList());

        if (bySymbol.Count == 0) return;

        // Load Company rows for these symbols in one query
        var symbols = bySymbol.Keys.ToList();
        var companies = await context.Companies
            .Where(c => symbols.Contains(c.Code))
            .ToListAsync(ct);

        foreach (var company in companies)
        {
            if (!bySymbol.TryGetValue(company.Code, out var trades))
                continue;

            var tradeIds = trades.Select(t => t.Id).ToArray();

            var followers = await context.UserCompanyFollows
                .Where(f => f.CompanyId == company.Id)
                .Join(context.Users.Where(u => u.EmailConfirmed),
                      f => f.UserId,
                      u => u.Id,
                      (f, u) => new { Follow = f, User = u })
                .ToListAsync(ct);

            if (followers.Count == 0)
                continue;

            var userIds = followers.Select(f => f.Follow.UserId).ToList();

            var prefs = await context.NotificationPreferences
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, ct);

            foreach (var follower in followers)
            {
                var userId = follower.Follow.UserId;
                var userEmail = follower.User.Email ?? string.Empty;

                var pref = prefs.TryGetValue(userId, out var p)
                    ? p
                    : new NotificationPreference
                    {
                        UserId = userId,
                        EmailEnabled = true,
                        DiscordEnabled = false
                    };

                // Email
                if (pref.EmailEnabled && !string.IsNullOrEmpty(userEmail))
                {
                    await SendWithLogAsync(
                        userId, company.Id, batchRunId, tradeIds, "email",
                        () => emailService.SendTradeNotificationAsync(
                            userEmail, company.Name, company.Code, trades, ct),
                        ct);
                }

                // Discord 
                if (pref.DiscordEnabled && !string.IsNullOrEmpty(pref.DiscordWebhookUrl))
                {
                    await SendWithLogAsync(
                        userId, company.Id, batchRunId, tradeIds, "discord",
                        async () =>
                        {
                            var ok = await discordService.SendTradeNotificationAsync(
                                pref.DiscordWebhookUrl!, company.Name, company.Code, trades, ct);
                            if (!ok) throw new Exception("Discord webhook returned failure");
                        },
                        ct);
                }

            }
        }

        logger.LogInformation(
            "Batch {BatchRunId} notifications processing complete for {Count} companies",
            batchRunId, companies.Count);
    }

    private async Task SendWithLogAsync(
        string userId,
        int companyId,
        string batchRunId,
        int[] transactionIds,
        string channel,
        Func<Task> send,
        CancellationToken ct)
    {
        var alreadySent = await context.NotificationLogs
            .AnyAsync(l =>
                l.UserId == userId &&
                l.CompanyId == companyId &&
                l.BatchRunId == batchRunId &&
                l.Channel == channel && 
                l.Success,
            ct);
            
        if (alreadySent)
        {
            logger.LogDebug(
                "Skipping duplicate {Channel} for user {UserId} company {CompanyId} batch {BatchRunId}",
                channel, userId, companyId, batchRunId);
            return;
        }

        string? errorMessage = null;
        bool success = false;

        try
        {
            await send();
            success = true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            logger.LogWarning(
                 "Failed to send {Channel} notification for user {UserId} company {CompanyId}: {Error}",
                channel, userId, companyId, ex.Message);
        }

        context.NotificationLogs.Add(new NotificationLog
        {
            UserId = userId,
            CompanyId = companyId,
            BatchRunId = batchRunId,
            TransactionIds = transactionIds,
            Channel = channel,
            SentAt = DateTime.UtcNow,
            Success = success,
            ErrorMessage = errorMessage
        });

        await context.SaveChangesAsync(ct);
    }
}
