using AktieKoll.Interfaces;
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

        return SendAsync(
            toEmail,
            "Verifiera din e-postadress – AktieKoll",
            $"""<p>Hej!</p>
               <p>Klicka på länken nedan för att verifiera din e-postadress:</p>
               <p><a href="{link}">{link}</a></p>
               <p>Länken är giltig i 24 timmar.</p>""",
            ct);
    }

    public Task SendPasswordResetAsync(string toEmail, string token, CancellationToken ct = default)
    {
        var encodedToken  = Uri.EscapeDataString(token);
        var encodedEmail  = Uri.EscapeDataString(toEmail);
        var link = $"{FrontendUrl}/auth/reset-password?email={encodedEmail}&token={encodedToken}";

        return SendAsync(
            toEmail,
            "Återställ ditt lösenord – AktieKoll",
            $"""<p>Hej!</p>
               <p>Vi fick en begäran om att återställa lösenordet för ditt konto.</p>
               <p><a href="{link}">Återställ lösenord</a></p>
               <p>Länken är giltig i 1 timme. Om du inte begärde detta kan du ignorera detta mejl.</p>""",
            ct);
    }

    public Task SendAccountDeletionRequestAsync(string toEmail, string deletionToken, CancellationToken ct = default)
    {
        var encodedToken = Uri.EscapeDataString(deletionToken);
        var link = $"{FrontendUrl}/auth/delete-confirm?token={encodedToken}";

        return SendAsync(
            toEmail,
            "Bekräfta borttagning av konto – AktieKoll",
            $"""<p>Hej!</p>
               <p>Vi fick en begäran om att permanent radera ditt AktieKoll-konto.</p>
               <p><strong>All din data kommer att raderas permanent och kan inte återställas.</strong></p>
               <p><a href="{link}">Bekräfta kontoborttagning</a></p>
               <p>Länken är giltig i 1 timme. Om du inte begärde detta, ignorera detta mejl – ditt konto är säkert.</p>""",
            ct);
    }

    public Task SendAccountDeletedConfirmationAsync(string toEmail, CancellationToken ct = default)
    {
        return SendAsync(
            toEmail,
            "Ditt konto har raderats – AktieKoll",
            """<p>Hej!</p>
               <p>Ditt AktieKoll-konto och all tillhörande data har nu raderats permanent i enlighet med GDPR.</p>
               <p>Vi hoppas att vi ses igen.</p>""",
            ct);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(FromName, FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = htmlBody };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.StartTls, ct);
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
