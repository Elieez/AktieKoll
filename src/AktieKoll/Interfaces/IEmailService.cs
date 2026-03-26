namespace AktieKoll.Interfaces;

/// <summary>Sends transactional emails for auth flows.</summary>
public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string userId, string token, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string token, CancellationToken ct = default);
    Task SendAccountDeletionRequestAsync(string toEmail, string deletionToken, CancellationToken ct = default);
    Task SendAccountDeletedConfirmationAsync(string toEmail, CancellationToken ct = default);
}
