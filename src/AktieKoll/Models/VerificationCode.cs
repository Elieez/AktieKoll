using System.ComponentModel.DataAnnotations;

namespace AktieKoll.Models;

/// <summary>
/// Short-lived code used to verify email addresses and reset passwords.
/// Maps a cryptographically-random 8-character code to the underlying
/// ASP.NET Identity token so that links sent in emails remain short.
/// </summary>
public class VerificationCode
{
    public int Id { get; set; }

    /// <summary>8-char uppercase alphanumeric code included in the email link.</summary>
    [Required, MaxLength(16)]
    public required string Code { get; set; }

    /// <summary>The user this code belongs to.</summary>
    [Required]
    public required string UserId { get; set; }

    /// <summary>The underlying ASP.NET Identity token (email confirmation or password reset).</summary>
    [Required]
    public required string Token { get; set; }

    /// <summary>"email_confirmation" or "password_reset".</summary>
    [Required, MaxLength(32)]
    public required string Purpose { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Prevents replay: once a code is consumed it cannot be used again.</summary>
    public bool Used { get; set; }
}
