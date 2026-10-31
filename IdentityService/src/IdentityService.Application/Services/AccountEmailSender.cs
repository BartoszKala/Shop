using System.Globalization;
using System.Text.Encodings.Web;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Common;
using IdentityService.Application.Options;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Services;

public sealed class AccountEmailSender(IEmailService emailService, IOptions<FrontendOptions> frontendOptions)
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly FrontendOptions _frontend = frontendOptions.Value;

    public Task SendEmailConfirmationAsync(ApplicationUser user, string token, CancellationToken ct = default)
    {
        var link = BuildLink(_frontend.ConfirmEmailPath,
            ("userId", user.Id.ToString()),
            ("code", TokenEncoding.Encode(token)));

        return emailService.SendAsync(user.Email!, "Potwierdź swój adres e-mail",
            $"""
             <p>Dziękujemy za rejestrację w naszym sklepie.</p>
             <p>Aby aktywować konto, <a href="{Html(link)}">kliknij tutaj i potwierdź adres e-mail</a>.</p>
             <p>Jeśli to nie Ty zakładałeś konto, zignoruj tę wiadomość.</p>
             """, ct);
    }

    public Task SendEmailChangeConfirmationAsync(ApplicationUser user, string newEmail, string token, CancellationToken ct = default)
    {
        var link = BuildLink(_frontend.ConfirmEmailChangePath,
            ("userId", user.Id.ToString()),
            ("email", newEmail),
            ("code", TokenEncoding.Encode(token)));

        return emailService.SendAsync(newEmail, "Potwierdź zmianę adresu e-mail",
            $"""
             <p>Otrzymaliśmy prośbę o zmianę adresu e-mail konta na <strong>{Html(newEmail)}</strong>.</p>
             <p><a href="{Html(link)}">Kliknij tutaj, aby potwierdzić zmianę</a>.</p>
             <p>Jeśli to nie Ty, zignoruj tę wiadomość - adres nie zostanie zmieniony.</p>
             """, ct);
    }

    public Task SendEmailChangedNotificationAsync(string oldEmail, string newEmail, CancellationToken ct = default) =>
        emailService.SendAsync(oldEmail, "Adres e-mail konta został zmieniony",
            $"""
             <p>Adres e-mail Twojego konta został zmieniony na <strong>{Html(newEmail)}</strong>.</p>
             <p>Jeśli to nie Ty, natychmiast skontaktuj się z obsługą sklepu.</p>
             """, ct);

    public Task SendPasswordResetAsync(ApplicationUser user, string token, bool forcedByAdmin, CancellationToken ct = default)
    {
        var link = BuildLink(_frontend.ResetPasswordPath,
            ("email", user.Email!),
            ("code", TokenEncoding.Encode(token)));

        var intro = forcedByAdmin
            ? "Ze względów bezpieczeństwa administrator sklepu zresetował Twoje hasło i wylogował wszystkie sesje."
            : "Otrzymaliśmy prośbę o zresetowanie hasła.";

        return emailService.SendAsync(user.Email!, "Ustaw nowe hasło",
            $"""
             <p>{intro}</p>
             <p><a href="{Html(link)}">Kliknij tutaj, aby ustawić nowe hasło</a>. Link jest ważny 24 godziny.</p>
             <p>Jeśli to nie Ty prosiłeś o reset, zignoruj tę wiadomość.</p>
             """, ct);
    }

    public Task SendPasswordChangedNotificationAsync(ApplicationUser user, CancellationToken ct = default) =>
        emailService.SendAsync(user.Email!, "Hasło zostało zmienione",
            """
            <p>Hasło do Twojego konta zostało zmienione, a pozostałe sesje wylogowane.</p>
            <p>Jeśli to nie Ty, natychmiast zresetuj hasło i skontaktuj się z obsługą sklepu.</p>
            """, ct);

    public Task SendTwoFactorChangedNotificationAsync(ApplicationUser user, bool enabled, CancellationToken ct = default) =>
        emailService.SendAsync(user.Email!,
            enabled ? "Włączono uwierzytelnianie dwuskładnikowe" : "Wyłączono uwierzytelnianie dwuskładnikowe",
            $"""
             <p>Uwierzytelnianie dwuskładnikowe dla Twojego konta zostało {(enabled ? "włączone" : "wyłączone")}.</p>
             <p>Jeśli to nie Ty, natychmiast zmień hasło.</p>
             """, ct);

    public Task SendNewDeviceLoginNotificationAsync(
        ApplicationUser user, DateTimeOffset at, string? ipAddress, string? userAgent, CancellationToken ct = default) =>
        emailService.SendAsync(user.Email!, "Nowe logowanie na Twoje konto",
            $"""
             <p>Zalogowano się na Twoje konto z nowego urządzenia.</p>
             <ul>
               <li>Kiedy: {Html(at.ToOffset(TimeSpan.Zero).ToString("dd.MM.yyyy HH:mm 'UTC'", Polish))}</li>
               <li>Adres IP: {Html(ipAddress ?? "nieznany")}</li>
               <li>Urządzenie: {Html(userAgent ?? "nieznane")}</li>
             </ul>
             <p>Jeśli to nie Ty, zmień hasło i wyloguj wszystkie sesje w ustawieniach konta.</p>
             """, ct);

    public Task SendAccountDeletedAsync(string email, CancellationToken ct = default) =>
        emailService.SendAsync(email, "Konto zostało usunięte",
            """
            <p>Twoje konto w naszym sklepie zostało usunięte, a dane osobowe skasowane.</p>
            <p>Dane, które musimy przechowywać z mocy prawa (np. faktury), zostaną usunięte po upływie wymaganego okresu.</p>
            """, ct);

    private string BuildLink(string path, params (string Key, string Value)[] query) =>
        _frontend.BaseUrl.TrimEnd('/') + path + "?" +
        string.Join('&', query.Select(q => $"{q.Key}={Uri.EscapeDataString(q.Value)}"));

    private static string Html(string value) => HtmlEncoder.Default.Encode(value);
}
