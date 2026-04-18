using AktieKoll.Models;

namespace AktieKoll.Interfaces;

/// <summary>Sends transactional emails for auth flows.</summary>
public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string code, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string code, CancellationToken ct = default);
    Task SendAccountDeletionRequestAsync(string toEmail, string deletionToken, CancellationToken ct = default);
    Task SendAccountDeletedConfirmationAsync(string toEmail, CancellationToken ct = default);
    Task SendTradeNotificationAsync(string toEmail, string companyName, string companyCode, List<InsiderTrade> trades, CancellationToken ct = default);
}
