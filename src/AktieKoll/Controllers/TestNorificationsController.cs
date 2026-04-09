using System.Security.Claims;
using AktieKoll.Interfaces;
using AktieKoll.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AktieKoll.Controllers
{
    [ApiController]
    [Route("api/test-notifications")]
    [Authorize]
    public class TestNotificationsController(
    IEmailService emailService,
    IDiscordService discordService,
    INotificationService notificationService,
    IWebHostEnvironment env) : ControllerBase
    {
        // Guard: endpoint is only reachable in Development or Staging
        private IActionResult? GuardNonProd()
        {
            if (env.IsProduction())
                return NotFound();
            return null;
        }

        private string? GetUserEmail() => User.FindFirstValue(ClaimTypes.Email);

        // ──────────────────────────────────────────────────────────────
        // Sample trade data
        // ──────────────────────────────────────────────────────────────

        private static List<InsiderTrade> SampleTrades() =>
        [
            new InsiderTrade
        {
            Id              = 1,
            CompanyName     = "Atlas Copco AB",
            InsiderName     = "Mats Rahmström",
            Position        = "VD (CEO)",
            TransactionType = "Förvärv",
            Shares          = 30_000,
            Price           = 176.40m,
            Currency        = "SEK",
            Status          = "Aktuell",
            Isin            = "SE0011166610",
            Symbol          = "ATCO-A",
            PublishingDate  = DateTime.UtcNow,
            TransactionDate = DateTime.UtcNow.Date
        },
        new InsiderTrade
        {
            Id              = 2,
            CompanyName     = "Atlas Copco AB",
            InsiderName     = "Peter Kinnart",
            Position        = "Styrelseledamot",
            TransactionType = "Avyttring",
            Shares          = 8_500,
            Price           = 178.90m,
            Currency        = "SEK",
            Status          = "Aktuell",
            Isin            = "SE0011166610",
            Symbol          = "ATCO-A",
            PublishingDate  = DateTime.UtcNow,
            TransactionDate = DateTime.UtcNow.Date
        }
        ];

        // ──────────────────────────────────────────────────────────────
        // POST /api/test-notifications/email
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a trade notification email directly to the authenticated user.
        /// Uses the same HTML template as the real notification pipeline.
        /// </summary>
        [HttpPost("email")]
        public async Task<IActionResult> TestEmail(CancellationToken ct)
        {
            if (GuardNonProd() is { } block) return block;

            var toEmail = GetUserEmail();
            if (string.IsNullOrEmpty(toEmail))
                return BadRequest(new { error = "No email claim on JWT." });

            await emailService.SendTradeNotificationAsync(
                toEmail, "Atlas Copco AB", "ATCO-A", SampleTrades(), ct);

            return Ok(new { sent = true, to = toEmail, trades = SampleTrades().Count });
        }

        // ──────────────────────────────────────────────────────────────
        // POST /api/test-notifications/discord
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a trade notification to a Discord webhook URL.
        /// Pass the webhook URL in the request body.
        /// </summary>
        [HttpPost("discord")]
        public async Task<IActionResult> TestDiscord([FromBody] TestDiscordRequest body, CancellationToken ct)
        {
            if (GuardNonProd() is { } block) return block;

            if (string.IsNullOrWhiteSpace(body.WebhookUrl))
                return BadRequest(new { error = "webhookUrl is required." });

            if (!Uri.TryCreate(body.WebhookUrl, UriKind.Absolute, out var uri) ||
                !uri.Host.Equals("discord.com", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "webhookUrl must be from discord.com." });

            var ok = await discordService.SendTradeNotificationAsync(
                body.WebhookUrl, "Atlas Copco AB", "ATCO-A", SampleTrades(), ct);

            return Ok(new { sent = ok, trades = SampleTrades().Count });
        }

        // ──────────────────────────────────────────────────────────────
        // POST /api/test-notifications/batch
        // ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Runs the full ProcessBatchNotificationsAsync pipeline exactly as FetchTrades does.
        /// The authenticated user must have at least one company follow and notification
        /// preferences configured — otherwise nothing is dispatched.
        /// The batchRunId is generated from the current minute so repeated calls within
        /// the same minute are idempotent (matching the real dedup behaviour).
        /// </summary>
        [HttpPost("batch")]
        public async Task<IActionResult> TestBatch(CancellationToken ct)
        {
            if (GuardNonProd() is { } block) return block;

            var batchRunId = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm");

            await notificationService.ProcessBatchNotificationsAsync(batchRunId, SampleTrades(), ct);

            return Ok(new
            {
                batchRunId,
                message = "Batch processed. Notifications dispatched to all followers of ATCO-A.",
                note = "If you are not following ATCO-A or have no notification preferences set, nothing was sent."
            });
        }
    }

    public record TestDiscordRequest(string WebhookUrl);
}
