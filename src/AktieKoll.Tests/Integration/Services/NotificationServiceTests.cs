using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using AktieKoll.Services;
using AktieKoll.Tests.Shared.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AktieKoll.Tests.Integration.Services;

public class NotificationServiceTests
{
    private static readonly DateTime TradeDate = new(2025, 6, 12, 0, 0, 0, DateTimeKind.Utc);
    private const string BatchRunId = "2025-06-12T06:00";

    // ──────────────────────────────────────────────
    // Real Atlas Copco insider trades (June 2025 style)
    // ──────────────────────────────────────────────

    private static List<InsiderTrade> AtlasTrades() =>
    [
        new InsiderTrade
        {
            CompanyName = "Atlas Copco AB",
            InsiderName = "Mats Rahmström",
            Position    = "VD (CEO)",
            TransactionType = "Förvärv",
            Shares   = 30_000,
            Price    = 176.40m,
            Currency = "SEK",
            Status   = "Aktuell",
            Isin     = "SE0011166610",
            Symbol   = "ATCO-A",
            PublishingDate  = TradeDate,
            TransactionDate = TradeDate
        },
        new InsiderTrade
        {
            CompanyName = "Atlas Copco AB",
            InsiderName = "Peter Kinnart",
            Position    = "Styrelseledamot",
            TransactionType = "Avyttring",
            Shares   = 8_500,
            Price    = 178.90m,
            Currency = "SEK",
            Status   = "Aktuell",
            Isin     = "SE0011166610",
            Symbol   = "ATCO-A",
            PublishingDate  = TradeDate,
            TransactionDate = TradeDate
        }
    ];

    // ──────────────────────────────────────────────
    // Setup helpers
    // ──────────────────────────────────────────────

    private static async Task<(ApplicationDbContext ctx, Company company)> SetupFollowerAsync(
        bool emailEnabled = true,
        bool discordEnabled = false,
        string? webhookUrl = null)
    {
        var ctx = ServiceTestHelpers.CreateContext();

        var company = new Company
        {
            Code = "ATCO-A",
            Name = "Atlas Copco AB",
            Isin = "SE0011166610",
            Currency = "SEK",
            Type = "Common Stock"
        };
        ctx.Companies.Add(company);

        var user = new ApplicationUser
        {
            Id = "user-1",
            UserName = "investor@example.com",
            NormalizedUserName = "INVESTOR@EXAMPLE.COM",
            Email = "investor@example.com",
            NormalizedEmail = "INVESTOR@EXAMPLE.COM",
            SecurityStamp = "stamp",
            ConcurrencyStamp = "stamp",
            DisplayName = "Lars Eriksson",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.UserCompanyFollows.Add(new UserCompanyFollow
        {
            UserId = "user-1",
            CompanyId = company.Id
        });
        ctx.NotificationPreferences.Add(new NotificationPreference
        {
            UserId = "user-1",
            EmailEnabled = emailEnabled,
            DiscordEnabled = discordEnabled,
            DiscordWebhookUrl = webhookUrl
        });
        await ctx.SaveChangesAsync();

        return (ctx, company);
    }

    private static (
        Mock<IEmailService> mock,
        List<(string ToEmail, string CompanyName, string CompanyCode, List<InsiderTrade> Trades)> captures)
        CreateEmailMock()
    {
        var captures = new List<(string, string, string, List<InsiderTrade>)>();
        var mock = new Mock<IEmailService>();
        mock.Setup(e => e.SendTradeNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<InsiderTrade>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, List<InsiderTrade>, CancellationToken>(
                (to, name, code, trades, _) => captures.Add((to, name, code, trades)))
            .Returns(Task.CompletedTask);
        return (mock, captures);
    }

    private static (
        Mock<IDiscordService> mock,
        List<(string WebhookUrl, string CompanyName, string CompanyCode, List<InsiderTrade> Trades)> captures)
        CreateDiscordMock(bool returns = true)
    {
        var captures = new List<(string, string, string, List<InsiderTrade>)>();
        var mock = new Mock<IDiscordService>();
        mock.Setup(d => d.SendTradeNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<InsiderTrade>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, List<InsiderTrade>, CancellationToken>(
                (url, name, code, trades, _) => captures.Add((url, name, code, trades)))
            .ReturnsAsync(returns);
        return (mock, captures);
    }

    // ──────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────

    /// <summary>
    /// Happy path: follower with email enabled receives the trade details
    /// and a success log entry is written to the DB.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_EmailFollower_SendsEmailAndLogsSuccess()
    {
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: true);
        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct); // populates trade IDs

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct);

        var logs = await ctx.NotificationLogs.ToListAsync(ServiceTestHelpers.Ct);

        await Verify(new
        {
            EmailsSent = emailCaptures.Select(c => new
            {
                c.ToEmail,
                c.CompanyName,
                c.CompanyCode,
                c.Trades
            }).ToList(),
            DiscordSent = Array.Empty<object>(),
            Logs = logs.Select(l => new
            {
                l.UserId,
                l.CompanyId,
                l.BatchRunId,
                l.TransactionIds,
                l.Channel,
                l.Success
            })
        });
    }

    /// <summary>
    /// Happy path: follower with Discord webhook receives the trade details
    /// and a success log entry is written.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_DiscordFollower_SendsDiscordAndLogsSuccess()
    {
        const string webhook = "https://discord.com/api/webhooks/123456789/real-token-xyz";
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: false, discordEnabled: true, webhookUrl: webhook);
        var (emailMock, _) = CreateEmailMock();
        var (discordMock, discordCaptures) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct);

        var logs = await ctx.NotificationLogs.ToListAsync(ServiceTestHelpers.Ct);

        await Verify(new
        {
            EmailsSent = Array.Empty<object>(),
            DiscordSent = discordCaptures.Select(c => new
            {
                c.WebhookUrl,
                c.CompanyName,
                c.CompanyCode,
                c.Trades
            }).ToList(),
            Logs = logs.Select(l => new
            {
                l.UserId,
                l.CompanyId,
                l.BatchRunId,
                l.TransactionIds,
                l.Channel,
                l.Success
            })
        });
    }

    /// <summary>
    /// When both email and Discord are enabled, both channels are dispatched
    /// and two separate log entries are created.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_BothChannels_BothSent()
    {
        const string webhook = "https://discord.com/api/webhooks/123456789/real-token-xyz";
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: true, discordEnabled: true, webhookUrl: webhook);
        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, discordCaptures) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct);

        var logs = await ctx.NotificationLogs.OrderBy(l => l.Channel).ToListAsync(ServiceTestHelpers.Ct);

        await Verify(new
        {
            EmailsSent = emailCaptures.Select(c => new { c.ToEmail, c.CompanyName, c.CompanyCode }).ToList(),
            DiscordSent = discordCaptures.Select(c => new { c.WebhookUrl, c.CompanyName, c.CompanyCode }).ToList(),
            Logs = logs.Select(l => new { l.UserId, l.Channel, l.BatchRunId, l.Success })
        });
    }

    /// <summary>
    /// Re-running with the same batchRunId must not re-send the notification.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_SameRunId_IsIdempotent()
    {
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: true);
        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct);
        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct); // second run, same id

        var logs = await ctx.NotificationLogs.ToListAsync(ServiceTestHelpers.Ct);

        Assert.Single(logs);           // only one log entry written
        Assert.Single(emailCaptures);  // email sent exactly once
    }

    /// <summary>
    /// A user with no NotificationPreference row should receive email by default
    /// (EmailEnabled = true is the default per the spec).
    /// </summary>
    [Fact]
    public async Task ProcessBatch_NoPreferenceRow_EmailEnabledByDefault()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        var company = new Company
        {
            Code = "ATCO-A",
            Name = "Atlas Copco AB",
            Isin = "SE0011166610",
            Currency = "SEK",
            Type = "Common Stock"
        };
        ctx.Companies.Add(company);
        ctx.Users.Add(new ApplicationUser
        {
            Id = "user-1",
            UserName = "investor@example.com",
            NormalizedUserName = "INVESTOR@EXAMPLE.COM",
            Email = "investor@example.com",
            NormalizedEmail = "INVESTOR@EXAMPLE.COM",
            SecurityStamp = "stamp",
            ConcurrencyStamp = "stamp",
            DisplayName = "Lars Eriksson",
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);
        ctx.UserCompanyFollows.Add(new UserCompanyFollow { UserId = "user-1", CompanyId = company.Id });
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);
        // No NotificationPreference row → defaults apply (EmailEnabled = true)

        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct);

        Assert.Single(emailCaptures);
        Assert.Equal("investor@example.com", emailCaptures[0].ToEmail);
        Assert.True((await ctx.NotificationLogs.FirstAsync(ServiceTestHelpers.Ct)).Success);
    }

    /// <summary>
    /// When no user follows the company, no notification is sent and no log is written.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_NoFollowers_NoNotificationsSent()
    {
        var ctx = ServiceTestHelpers.CreateContext();
        ctx.Companies.Add(new Company
        {
            Code = "ATCO-A",
            Name = "Atlas Copco AB",
            Isin = "SE0011166610",
            Currency = "SEK",
            Type = "Common Stock"
        });
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, AtlasTrades(), ServiceTestHelpers.Ct);

        Assert.Empty(emailCaptures);
        Assert.Empty(await ctx.NotificationLogs.ToListAsync(ServiceTestHelpers.Ct));
    }

    /// <summary>
    /// When the email service throws, the failure is recorded in the log
    /// and the exception does not propagate to the caller.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_EmailFails_LogsFailureAndDoesNotThrow()
    {
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: true);

        var emailMock = new Mock<IEmailService>();
        emailMock.Setup(e => e.SendTradeNotificationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<List<InsiderTrade>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection failed"));

        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var trades = AtlasTrades();
        ctx.InsiderTrades.AddRange(trades);
        await ctx.SaveChangesAsync(ServiceTestHelpers.Ct);

        await svc.ProcessBatchNotificationsAsync(BatchRunId, trades, ServiceTestHelpers.Ct); // must not throw

        var log = await ctx.NotificationLogs.SingleAsync(ServiceTestHelpers.Ct);
        Assert.False(log.Success);
        Assert.Equal("SMTP connection failed", log.ErrorMessage);
    }

    /// <summary>
    /// A trade whose Symbol is null or empty is excluded from processing.
    /// </summary>
    [Fact]
    public async Task ProcessBatch_TradeWithoutSymbol_IsSkipped()
    {
        var (ctx, _) = await SetupFollowerAsync(emailEnabled: true);
        var (emailMock, emailCaptures) = CreateEmailMock();
        var (discordMock, _) = CreateDiscordMock();
        var svc = new NotificationService(ctx, emailMock.Object, discordMock.Object,
            NullLogger<NotificationService>.Instance);

        var tradesWithoutSymbol = new List<InsiderTrade>
        {
            new InsiderTrade
            {
                CompanyName = "Atlas Copco AB",
                InsiderName = "Mats Rahmström",
                Position    = "VD (CEO)",
                TransactionType = "Förvärv",
                Shares = 5_000,
                Price  = 176.40m,
                Currency = "SEK",
                Status = "Aktuell",
                Symbol = null, // no symbol resolved
                PublishingDate  = TradeDate,
                TransactionDate = TradeDate
            }
        };

        await svc.ProcessBatchNotificationsAsync(BatchRunId, tradesWithoutSymbol, ServiceTestHelpers.Ct);

        Assert.Empty(emailCaptures);
        Assert.Empty(await ctx.NotificationLogs.ToListAsync(ServiceTestHelpers.Ct));
    }
}