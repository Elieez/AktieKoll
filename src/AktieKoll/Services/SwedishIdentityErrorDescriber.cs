using Microsoft.AspNetCore.Identity;

namespace AktieKoll.Services;

/// <summary>
/// Returns Swedish error messages for ASP.NET Identity validation failures,
/// so the frontend can display them directly to the user.
/// </summary>
public class SwedishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordTooShort(int length) => new()
    {
        Code = nameof(PasswordTooShort),
        Description = $"Lösenordet måste vara minst {length} tecken långt."
    };

    public override IdentityError PasswordRequiresUpper() => new()
    {
        Code = nameof(PasswordRequiresUpper),
        Description = "Lösenordet måste innehålla minst en stor bokstav (A–Z)."
    };

    public override IdentityError PasswordRequiresLower() => new()
    {
        Code = nameof(PasswordRequiresLower),
        Description = "Lösenordet måste innehålla minst en liten bokstav (a–z)."
    };

    public override IdentityError PasswordRequiresDigit() => new()
    {
        Code = nameof(PasswordRequiresDigit),
        Description = "Lösenordet måste innehålla minst en siffra (0–9)."
    };

    public override IdentityError PasswordRequiresNonAlphanumeric() => new()
    {
        Code = nameof(PasswordRequiresNonAlphanumeric),
        Description = "Lösenordet måste innehålla minst ett specialtecken (t.ex. !, @, #)."
    };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new()
    {
        Code = nameof(PasswordRequiresUniqueChars),
        Description = $"Lösenordet måste innehålla minst {uniqueChars} olika tecken."
    };

    public override IdentityError DuplicateEmail(string email) => new()
    {
        Code = nameof(DuplicateEmail),
        Description = "En användare med den e-postadressen finns redan."
    };

    public override IdentityError DuplicateUserName(string userName) => new()
    {
        Code = nameof(DuplicateUserName),
        Description = "En användare med det användarnamnet finns redan."
    };

    public override IdentityError InvalidEmail(string? email) => new()
    {
        Code = nameof(InvalidEmail),
        Description = "Ogiltig e-postadress."
    };

    public override IdentityError InvalidUserName(string? userName) => new()
    {
        Code = nameof(InvalidUserName),
        Description = "Ogiltigt användarnamn."
    };
}
