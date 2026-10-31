using IdentityService.Domain.Common;

namespace IdentityService.Application.Common;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = Error.Unauthorized("auth.invalid_credentials", "Nieprawidłowy e-mail lub hasło.");
    public static readonly Error LockedOut = Error.Forbidden("auth.locked_out", "Konto zostało tymczasowo zablokowane. Spróbuj później.");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("auth.email_not_confirmed", "Adres e-mail nie został potwierdzony.");
    public static readonly Error TwoFactorSessionExpired = Error.Unauthorized("auth.two_factor_session_expired", "Sesja logowania wygasła. Zaloguj się ponownie.");
    public static readonly Error InvalidTwoFactorCode = Error.Unauthorized("auth.invalid_two_factor_code", "Nieprawidłowy kod uwierzytelniający.");
    public static readonly Error InvalidRecoveryCode = Error.Unauthorized("auth.invalid_recovery_code", "Nieprawidłowy kod odzyskiwania.");
    public static readonly Error SessionExpired = Error.Unauthorized("auth.session_expired", "Sesja wygasła. Zaloguj się ponownie.");
    public static readonly Error InvalidLink = Error.BadRequest("auth.invalid_link", "Link jest nieprawidłowy lub wygasł.");
}

public static class AccountErrors
{
    public static readonly Error NotFound = Error.Unauthorized("account.not_found", "Konto nie istnieje.");
    public static readonly Error InvalidPassword = Error.BadRequest("account.invalid_password", "Nieprawidłowe hasło.");
    public static readonly Error SameEmail = Error.BadRequest("account.same_email", "Nowy adres jest taki sam jak obecny.");
    public static readonly Error EmailTaken = Error.Conflict("account.email_taken", "Ten adres e-mail jest już używany.");
    public static readonly Error SessionNotFound = Error.NotFound("account.session_not_found", "Sesja nie istnieje lub została już zakończona.");
}

public static class TwoFactorErrors
{
    public static readonly Error AlreadyEnabled = Error.Conflict("two_factor.already_enabled", "Uwierzytelnianie dwuskładnikowe jest już włączone.");
    public static readonly Error NotEnabled = Error.BadRequest("two_factor.not_enabled", "Uwierzytelnianie dwuskładnikowe nie jest włączone.");
    public static readonly Error NotConfigured = Error.BadRequest("two_factor.not_configured", "Najpierw skonfiguruj aplikację uwierzytelniającą.");
    public static readonly Error InvalidCode = Error.BadRequest("two_factor.invalid_code", "Nieprawidłowy kod. Sprawdź, czy czas na telefonie jest poprawny.");
}

public static class AdminErrors
{
    public static readonly Error UserNotFound = Error.NotFound("admin.user_not_found", "Użytkownik nie istnieje.");
    public static readonly Error CannotModifySelf = Error.BadRequest("admin.cannot_modify_self", "Nie możesz zablokować siebie ani odebrać sobie roli administratora.");
}
