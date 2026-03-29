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
    private string SmtpHost     => config["Email:SmtpHost"]     ?? throw new InvalidOperationException("Email:SmtpHost missing");
    private int    SmtpPort     => config.GetValue<int>("Email:SmtpPort", 587);
    private string SmtpUser     => config["Email:SmtpUsername"] ?? string.Empty;
    private string SmtpPass     => config["Email:SmtpPassword"] ?? string.Empty;
    private string FromAddress   => config["Email:FromAddress"]  ?? throw new InvalidOperationException("Email:FromAddress missing");
    private string FromName      => config["Email:FromName"]     ?? "AktieKoll";
    private string FrontendUrl   => (config["Frontend:Url"]      ?? "http://localhost:3000").TrimEnd('/');

    public Task SendEmailVerificationAsync(string toEmail, string userId, string token, CancellationToken ct = default)
    {
        var encodedToken = Uri.EscapeDataString(token);
        var link = $"{FrontendUrl}/auth/verify-email?userId={Uri.EscapeDataString(userId)}&token={encodedToken}";
        var html = $@"<p>Hej!</p>
               <p>Klicka på länken nedan för att verifiera din e-postadress:</p>
               <p><a href=""{link}"">{link}</a></p>
               <p>Länken är giltig i 24 timmar.</p>";

        return SendAsync(
            toEmail,
            "Verifiera din e-postadress – AktieKoll",
            html,
            ct);
    }

    public Task SendPasswordResetAsync(string toEmail, string token, CancellationToken ct = default)
    {
        var encodedToken  = Uri.EscapeDataString(token);
        var encodedEmail  = Uri.EscapeDataString(toEmail);
        var link = $"{FrontendUrl}/auth/reset-password?email={encodedEmail}&token={encodedToken}";
        var html = $@"<p>Hej!</p>
               <p>Vi fick en begäran om att återställa lösenordet för ditt konto.</p>
               <p><a href=""{link}"">Återställ lösenord</a></p>
               <p>Länken är giltig i 1 timme. Om du inte begärde detta kan du ignorera detta mejl.</p>";

        return SendAsync(
            toEmail,
            "Återställ ditt lösenord – AktieKoll",
            html,
            ct);
    }

    public Task SendAccountDeletionRequestAsync(string toEmail, string deletionToken, CancellationToken ct = default)
    {
        var encodedToken = Uri.EscapeDataString(deletionToken);
        var link = $"{FrontendUrl}/auth/delete-confirm?token={encodedToken}";
        var html = $@"<p>Hej!</p>
               <p>Vi fick en begäran om att permanent radera ditt AktieKoll-konto.</p>
               <p><strong>All din data kommer att raderas permanent och kan inte återställas.</strong></p>
               <p><a href=""{link}"">Bekräfta kontoborttagning</a></p>
               <p>Länken är giltig i 1 timme. Om du inte begärde detta, ignorera detta mejl – ditt konto är säkert.</p>";

        return SendAsync(
            toEmail,
            "Bekräfta borttagning av konto – AktieKoll",
            html,
            ct);
    }

    public Task SendAccountDeletedConfirmationAsync(string toEmail, CancellationToken ct = default)
    {
        var html = $@"<p>Hej!</p>
               <p>Ditt AktieKoll-konto och all tillhörande data har nu raderats permanent i enlighet med GDPR.</p>
               <p>Vi hoppas att vi ses igen.</p>";

        return SendAsync(
            toEmail,
            "Ditt konto har raderats – AktieKoll",
            html,
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
        var html = BuildTradeNotificationHtml(companyCode, companyCode, trades, stockUrl, settingsUrl);

        return SendAsync(
            toEmail,
            $"Ny insiderhandel: {companyName} - AktieKoll",
            html,
            ct);
    }

    private static string BuildTradeNotificationHtml(
        string companyName,
        string companyCode,
        List<InsiderTrade> trades,
        string stockUrl,
        string settingsUrl)
    {
        var rows = string.Concat(trades.Select(t =>
        {
            var typeLabel = t.TransactionType.ToLower() switch
            {
                var s when s.Contains("förvärv") => "<span style=\"color:#4deba8;font-weight:bold;\">KÖP</span>",
                var s when s.Contains("avyttring") => "<span style=\"color:#f06b4d;font-weight:bold;\">SÄLJ</span>",
                _ => System.Net.WebUtility.HtmlEncode(t.TransactionType)
            };

            var value = (t.Price * t.Shares).ToString("N0");
            return $@"
        <tr>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#cccccc;"">{System.Net.WebUtility.HtmlEncode(t.InsiderName)}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#aaaaaa;"">{System.Net.WebUtility.HtmlEncode(t.Position ?? "–")}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;"">{typeLabel}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#cccccc;text-align:right;"">{t.Shares:N0}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#cccccc;text-align:right;"">{t.Price:N2} {t.Currency}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#cccccc;text-align:right;"">{value} {t.Currency}</td>
          <td style=""padding:10px 12px;border-bottom:1px solid #2a2a2a;font-size:13px;color:#888888;"">{t.TransactionDate:yyyy-MM-dd}</td>
        </tr>";
        }));

        var companyNameEncoded = System.Net.WebUtility.HtmlEncode(companyName);
        var companyCodeEncoded = System.Net.WebUtility.HtmlEncode(companyCode);

        return $@"<!DOCTYPE html>
            <html lang=""sv"">
            <head>
              <meta charset=""UTF-8"">
              <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
              <title>Ny insiderhandel – {companyNameEncoded}</title>
            </head>
            <body style=""margin:0;padding:0;background:#0f0f0f;font-family:'Segoe UI',Arial,sans-serif;"">
              <div style=""max-width:680px;margin:32px auto;background:#1a1a1a;border-radius:12px;border:1px solid #2a2a2a;overflow:hidden;"">

                <!-- Header -->
                <div style=""background:#161616;padding:24px 28px;border-bottom:1px solid #2a2a2a;"">
                  <p style=""margin:0 0 4px;font-size:11px;text-transform:uppercase;letter-spacing:0.08em;color:#666;"">AktieKoll · Insiderbevakning</p>
                  <h1 style=""margin:0;font-size:20px;font-weight:700;color:#f0f0f0;"">Ny insiderhandel</h1>
                </div>

                <!-- Company heading -->
                <div style=""padding:20px 28px 12px;"">
                  <h2 style=""margin:0;font-size:16px;font-weight:600;color:#f0f0f0;"">
                    <a href=""{stockUrl}"" style=""color:#4deba8;text-decoration:none;"">{companyNameEncoded}</a>
                    <span style=""font-weight:400;font-size:13px;color:#666;""> &nbsp;{companyCodeEncoded}</span>
                  </h2>
                  <p style=""margin:6px 0 0;font-size:13px;color:#888;"">{trades.Count} transaktion(er) registrerade</p>
                </div>

                <!-- Table -->
                <div style=""padding:0 16px 20px;overflow-x:auto;"">
                  <table style=""width:100%;border-collapse:collapse;font-size:13px;"">
                    <thead>
                      <tr style=""background:#222;"">
                        <th style=""padding:8px 12px;text-align:left;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Insider</th>
                        <th style=""padding:8px 12px;text-align:left;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Roll</th>
                        <th style=""padding:8px 12px;text-align:left;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Typ</th>
                        <th style=""padding:8px 12px;text-align:right;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Antal</th>
                        <th style=""padding:8px 12px;text-align:right;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Pris</th>
                        <th style=""padding:8px 12px;text-align:right;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Värde</th>
                        <th style=""padding:8px 12px;text-align:left;font-size:11px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:#666;"">Datum</th>
                      </tr>
                    </thead>
                    <tbody>
                      {rows}
                    </tbody>
                  </table>
                </div>

                <!-- CTA -->
                <div style=""padding:16px 28px 24px;text-align:center;"">
                  <a href=""{stockUrl}""
                     style=""display:inline-block;background:#4deba8;color:#0f0f0f;padding:10px 24px;border-radius:8px;font-weight:700;font-size:13px;text-decoration:none;"">
                    Se alla transaktioner →
                  </a>
                </div>

                <!-- Footer -->
                <div style=""background:#111;padding:16px 28px;border-top:1px solid #222;text-align:center;"">
                  <p style=""margin:0;font-size:11px;color:#555;"">
                    Du får detta mejl för att du bevakar {companyNameEncoded} på AktieKoll.
                    <br>
                    <a href=""{settingsUrl}"" style=""color:#4deba8;text-decoration:none;"">Hantera notifikationer</a>
                  </p>
                </div>

              </div>
            </body>
            </html>";
        }

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
