using AktieKoll.Interfaces;
using AktieKoll.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace AktieKoll.Services;

/// <summary>
/// SMTP email service backed by MailKit.
/// Required environment variables / config:
///   Email__SmtpHost, Email__SmtpPort, Email__SmtpUsername, Email__SmtpPassword
///   Email__FromAddress, Email__FromName, Frontend__Url
/// </summary>
public class EmailService(IConfiguration config, ILogger<EmailService> logger) : IEmailService
{
    private string SmtpHost => config["Email:SmtpHost"] ?? throw new InvalidOperationException("Email:SmtpHost missing");
    private int SmtpPort => config.GetValue<int>("Email:SmtpPort", 587);
    private string SmtpUser => config["Email:SmtpUsername"] ?? string.Empty;
    private string SmtpPass => config["Email:SmtpPassword"] ?? string.Empty;
    private string FromAddress => config["Email:FromAddress"] ?? throw new InvalidOperationException("Email:FromAddress missing");
    private string FromName => config["Email:FromName"] ?? "AktieKoll";
    private string FrontendUrl => (config["Frontend:Url"] ?? "http://localhost:3000").TrimEnd('/');

    public Task SendEmailVerificationAsync(string toEmail, string code, CancellationToken ct = default)
    {
        var link = $"{FrontendUrl}/auth/verify-email?code={Uri.EscapeDataString(code)}";
        var content = $"""
            <p style="margin:0 0 12px;font-size:15px;color:#374151;">Välkommen till AktieKoll!</p>
            <p style="margin:0 0 20px;font-size:14px;color:#6b7280;">Klicka på knappen nedan för att verifiera din e-postadress och aktivera ditt konto.</p>
            {BuildCta(link, "Verifiera e-postadress")}
            """;
        var footer = "Länken är giltig i 24 timmar.<br>Om du inte skapade ett konto på AktieKoll kan du ignorera detta mejl.";

        return SendAsync(
            toEmail,
            "Verifiera din e-postadress – AktieKoll",
            BuildEmailLayout("Verifiera e-postadress", content, footer),
            ct);
    }

    public Task SendPasswordResetAsync(string toEmail, string code, CancellationToken ct = default)
    {
        var link = $"{FrontendUrl}/auth/reset-password?code={Uri.EscapeDataString(code)}";
        var content = $"""
            <p style="margin:0 0 12px;font-size:15px;color:#374151;">Vi fick en begäran om att återställa lösenordet för ditt konto.</p>
            <p style="margin:0 0 20px;font-size:14px;color:#6b7280;">Klicka på knappen nedan för att välja ett nytt lösenord.</p>
            {BuildCta(link, "Återställ lösenord")}
            """;
        var footer = "Länken är giltig i 1 timme.<br>Om du inte begärde detta kan du ignorera detta mejl – ditt konto är säkert.";

        return SendAsync(
            toEmail,
            "Återställ ditt lösenord – AktieKoll",
            BuildEmailLayout("Återställ lösenord", content, footer),
            ct);
    }

    public Task SendAccountDeletionRequestAsync(string toEmail, string deletionToken, CancellationToken ct = default)
    {
        var link = $"{FrontendUrl}/auth/delete-confirm?token={Uri.EscapeDataString(deletionToken)}";
        var content = $"""
            <p style="margin:0 0 12px;font-size:15px;color:#374151;">Vi fick en begäran om att permanent radera ditt AktieKoll-konto.</p>
            <p style="margin:0 0 20px;font-size:14px;font-weight:600;color:#dc2626;">All din data kommer att raderas permanent och kan inte återställas.</p>
            {BuildCta(link, "Bekräfta borttagning", "#dc2626")}
            """;
        var footer = "Länken är giltig i 1 timme.<br>Om du inte begärde detta, ignorera detta mejl – ditt konto är säkert.";

        return SendAsync(
            toEmail,
            "Bekräfta borttagning av konto – AktieKoll",
            BuildEmailLayout("Radera konto", content, footer),
            ct);
    }

    public Task SendAccountDeletedConfirmationAsync(string toEmail, CancellationToken ct = default)
    {
        var content = """
            <p style="margin:0 0 12px;font-size:15px;color:#374151;">Ditt AktieKoll-konto och all tillhörande data har nu raderats permanent i enlighet med GDPR.</p>
            <p style="margin:0;font-size:14px;color:#6b7280;">Vi hoppas att vi ses igen.</p>
            """;
        var footer = "Detta är en automatisk bekräftelse. Inget svar behövs.";

        return SendAsync(
            toEmail,
            "Ditt konto har raderats – AktieKoll",
            BuildEmailLayout("Konto raderat", content, footer),
            ct);
    }

    public Task SendTradeNotificationAsync(
        string toEmail,
        string companyName,
        string companyCode,
        List<InsiderTrade> trades,
        CancellationToken ct = default)
    {
        var stockUrl = $"{FrontendUrl}/stocks/{Uri.EscapeDataString(companyCode)}";
        var settingsUrl = $"{FrontendUrl}/settings";
        var html = BuildTradeNotificationHtml(companyName, companyCode, trades, stockUrl, settingsUrl);

        return SendAsync(
            toEmail,
            $"Ny insiderhandel: {companyName} - AktieKoll",
            html,
            ct);
    }

    // ─────────────────────────────────────────────────────────────
    // Shared HTML helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Wraps <paramref name="contentHtml"/> in the standard AktieKoll email shell:
    /// branded header → white content card → gray footer.
    /// </summary>
    private static string BuildEmailLayout(string title, string contentHtml, string footerHtml)
    {
        var titleEncoded = System.Net.WebUtility.HtmlEncode(title);
        return $"""
            <!DOCTYPE html>
            <html lang="sv">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{titleEncoded}</title>
            </head>
            <body style="margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f5f5f5;">
                <tr>
                  <td align="center" style="padding:32px 16px;">
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="max-width:600px;background:#ffffff;border-radius:8px;border:1px solid #e5e7eb;box-shadow:0 1px 4px rgba(0,0,0,0.06);">

                      <!-- Header -->
                      <tr>
                        <td style="background:#f9f9f9;padding:24px 24px 20px;border-bottom:1px solid #e5e7eb;border-radius:8px 8px 0 0;">
                          <p style="margin:0 0 4px;font-size:11px;text-transform:uppercase;letter-spacing:0.08em;color:#9ca3af;">AktieKoll</p>
                          <h1 style="margin:0;font-size:20px;font-weight:700;color:#111827;">{titleEncoded}</h1>
                        </td>
                      </tr>

                      <!-- Content -->
                      <tr>
                        <td style="padding:28px 24px 24px;">
                          {contentHtml}
                        </td>
                      </tr>

                      <!-- Footer -->
                      <tr>
                        <td style="background:#f9f9f9;padding:16px 24px;border-top:1px solid #e5e7eb;border-radius:0 0 8px 8px;text-align:center;">
                          <p style="margin:0;font-size:11px;color:#9ca3af;line-height:1.6;">{footerHtml}</p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    /// <summary>Returns a styled CTA button anchored to <paramref name="href"/>.</summary>
    private static string BuildCta(string href, string label, string bgColor = "#4deba8")
    {
        var textColor = bgColor == "#4deba8" ? "#0f0f0f" : "#ffffff";
        var labelEncoded = System.Net.WebUtility.HtmlEncode(label);
        return $"""<a href="{href}" style="display:inline-block;background:{bgColor};color:{textColor};padding:12px 32px;border-radius:8px;font-weight:700;font-size:14px;text-decoration:none;">{labelEncoded} &#8594;</a>""";
    }

    // ─────────────────────────────────────────────────────────────
    // Trade notification HTML (dedicated template)
    // ─────────────────────────────────────────────────────────────

    private static string BuildTradeNotificationHtml(
        string companyName,
        string companyCode,
        List<InsiderTrade> trades,
        string stockUrl,
        string settingsUrl)
    {
        var rows = string.Concat(trades.Select(t =>
        {
            var isKop = t.TransactionType.ToLower().Contains("förvärv");
            var isSalj = t.TransactionType.ToLower().Contains("avyttring");

            var typeBadge = isKop
                ? "<span style=\"display:inline-block;background:#059669;color:#ffffff;font-size:11px;font-weight:700;padding:2px 8px;border-radius:20px;letter-spacing:0.04em;\">KÖP</span>"
                : isSalj
                    ? "<span style=\"display:inline-block;background:#dc2626;color:#ffffff;font-size:11px;font-weight:700;padding:2px 8px;border-radius:20px;letter-spacing:0.04em;\">SÄLJ</span>"
                    : $"<span style=\"font-size:12px;color:#111827;\">{System.Net.WebUtility.HtmlEncode(t.TransactionType)}</span>";

            var value = (t.Price * t.Shares).ToString("N0", new System.Globalization.CultureInfo("sv-SE"));
            var shares = t.Shares.ToString("N0", new System.Globalization.CultureInfo("sv-SE"));
            var insiderEncoded = System.Net.WebUtility.HtmlEncode(t.InsiderName);
            var positionEncoded = System.Net.WebUtility.HtmlEncode(t.Position ?? "–");

            return $@"
                <div style=""border:1px solid #e5e7eb;border-radius:6px;margin-bottom:12px;overflow:hidden;"">
                  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;"">
                    <tr>
                      <td style=""padding:12px 14px 6px;font-weight:600;color:#111827;font-size:14px;"">{insiderEncoded}</td>
                      <td style=""padding:12px 14px 6px;text-align:right;color:#6b7280;font-size:13px;"">{positionEncoded}</td>
                    </tr>
                    <tr>
                      <td style=""padding:0 14px 10px;"">{typeBadge}</td>
                      <td style=""padding:0 14px 10px;text-align:right;color:#9ca3af;font-size:12px;"">{t.TransactionDate:yyyy-MM-dd}</td>
                    </tr>
                  </table>
                  <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""border-collapse:collapse;border-top:1px solid #e5e7eb;"">
                    <tr>
                      <td style=""padding:10px 14px;width:33%;"">
                        <div style=""font-size:11px;color:#9ca3af;text-transform:uppercase;letter-spacing:0.05em;margin-bottom:2px;"">Antal</div>
                        <div style=""font-size:13px;font-weight:600;color:#111827;"">{shares}</div>
                      </td>
                      <td style=""padding:10px 14px;width:33%;border-left:1px solid #e5e7eb;"">
                        <div style=""font-size:11px;color:#9ca3af;text-transform:uppercase;letter-spacing:0.05em;margin-bottom:2px;"">Pris</div>
                        <div style=""font-size:13px;font-weight:600;color:#111827;"">{t.Price:N2} {System.Net.WebUtility.HtmlEncode(t.Currency)}</div>
                      </td>
                      <td style=""padding:10px 14px;width:34%;border-left:1px solid #e5e7eb;"">
                        <div style=""font-size:11px;color:#9ca3af;text-transform:uppercase;letter-spacing:0.05em;margin-bottom:2px;"">V&#228;rde</div>
                        <div style=""font-size:13px;font-weight:600;color:#111827;"">{value} {System.Net.WebUtility.HtmlEncode(t.Currency)}</div>
                      </td>
                    </tr>
                  </table>
                </div>";
        }));

        var companyNameEncoded = System.Net.WebUtility.HtmlEncode(companyName);
        var companyCodeEncoded = System.Net.WebUtility.HtmlEncode(companyCode);

        return $@"<!DOCTYPE html>
            <html lang=""sv"">
            <head>
              <meta charset=""UTF-8"">
              <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
              <title>Ny insiderhandel &#8211; {companyNameEncoded}</title>
            </head>
            <body style=""margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;"">
              <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f5f5f5;"">
                <tr>
                  <td align=""center"" style=""padding:32px 16px;"">
                    <table width=""100%"" cellpadding=""0"" cellspacing=""0""
                           style=""max-width:600px;background:#ffffff;border-radius:8px;border:1px solid #e5e7eb;box-shadow:0 1px 4px rgba(0,0,0,0.06);"">

                      <!-- Header -->
                      <tr>
                        <td style=""background:#f9f9f9;padding:24px 24px 20px;border-bottom:1px solid #e5e7eb;border-radius:8px 8px 0 0;"">
                          <p style=""margin:0 0 4px;font-size:11px;text-transform:uppercase;letter-spacing:0.08em;color:#9ca3af;"">AktieKoll &middot; Insiderbevakning</p>
                          <h1 style=""margin:0;font-size:20px;font-weight:700;color:#111827;"">Ny insiderhandel</h1>
                        </td>
                      </tr>

                      <!-- Company -->
                      <tr>
                        <td style=""padding:20px 24px 16px;"">
                          <h2 style=""margin:0;font-size:16px;font-weight:600;color:#111827;"">
                            <a href=""{stockUrl}"" style=""color:#059669;text-decoration:none;"">{companyNameEncoded}</a>
                            <span style=""font-weight:400;font-size:13px;color:#6b7280;"">&nbsp;{companyCodeEncoded}</span>
                          </h2>
                          <p style=""margin:6px 0 0;font-size:13px;color:#6b7280;"">{trades.Count} transaktion(er) registrerade</p>
                        </td>
                      </tr>

                      <!-- Trade cards -->
                      <tr>
                        <td style=""padding:0 24px 8px;"">
                          {rows}
                        </td>
                      </tr>

                      <!-- CTA -->
                      <tr>
                        <td style=""padding:16px 24px 24px;text-align:center;"">
                          <a href=""{stockUrl}""
                             style=""display:inline-block;background:#4deba8;color:#0f0f0f;padding:11px 28px;border-radius:8px;font-weight:700;font-size:13px;text-decoration:none;"">
                            Se alla transaktioner &#8594;
                          </a>
                        </td>
                      </tr>

                      <!-- Footer -->
                      <tr>
                        <td style=""background:#f9f9f9;padding:16px 24px;border-top:1px solid #e5e7eb;border-radius:0 0 8px 8px;text-align:center;"">
                          <p style=""margin:0;font-size:11px;color:#9ca3af;"">
                            Du f&#229;r detta mejl f&#246;r att du bevakar {companyNameEncoded} p&#229; AktieKoll.<br>
                            <a href=""{settingsUrl}"" style=""color:#059669;text-decoration:none;"">Hantera notifikationer</a>
                          </p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>";
    }

    // ─────────────────────────────────────────────────────────────
    // SMTP send
    // ─────────────────────────────────────────────────────────────

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(FromName, FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        var secureOptions = SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : SmtpPort == 25 || SmtpPort == 1025
                ? SecureSocketOptions.None
                : SecureSocketOptions.StartTls;

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, secureOptions, ct);
            if (!string.IsNullOrEmpty(SmtpUser))
                await client.AuthenticateAsync(SmtpUser, SmtpPass, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email to {Email} with subject '{Subject}'", toEmail, subject);
            throw;
        }
    }
}
