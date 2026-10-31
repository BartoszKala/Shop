using Microsoft.AspNetCore.Identity;

namespace IdentityService.Infrastructure.Identity;

internal sealed class PolishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "Wystąpił nieznany błąd.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Dane zostały zmienione w międzyczasie. Spróbuj ponownie.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "Nieprawidłowe hasło.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "Link jest nieprawidłowy lub wygasł.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "Nieprawidłowy kod odzyskiwania.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "To konto zewnętrzne jest już powiązane z innym użytkownikiem.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), $"Nazwa użytkownika '{userName}' jest nieprawidłowa.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), $"Adres e-mail '{email}' jest nieprawidłowy.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateEmail), $"Konto z adresem '{userName}' już istnieje.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), $"Konto z adresem '{email}' już istnieje.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), $"Nazwa roli '{role}' jest nieprawidłowa.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), $"Rola '{role}' już istnieje.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "Użytkownik ma już ustawione hasło.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "Blokada konta nie jest włączona dla tego użytkownika.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), $"Użytkownik ma już rolę '{role}'.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), $"Użytkownik nie ma roli '{role}'.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Hasło musi mieć co najmniej {length} znaków.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Hasło musi zawierać co najmniej {uniqueChars} różnych znaków.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Hasło musi zawierać co najmniej jeden znak specjalny.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Hasło musi zawierać co najmniej jedną cyfrę.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Hasło musi zawierać co najmniej jedną małą literę.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Hasło musi zawierać co najmniej jedną wielką literę.");

    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
}
