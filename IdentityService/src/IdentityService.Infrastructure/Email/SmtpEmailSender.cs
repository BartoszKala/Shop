using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IdentityService.Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;

    public string? UserName { get; set; }
    public string? Password { get; set; }

    [Required, EmailAddress]
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Sklep";
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody);

public interface IEmailDelivery
{
    Task DeliverAsync(EmailMessage message, CancellationToken ct = default);
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailDelivery
{
    public async Task DeliverAsync(EmailMessage email, CancellationToken ct = default)
    {
        var smtp = options.Value;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email.To));
        message.Subject = email.Subject;
        message.Body = new BodyBuilder { HtmlBody = WrapInLayout(email.HtmlBody, smtp.FromName) }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, smtp.Security, ct);
        if (!string.IsNullOrEmpty(smtp.UserName))
        {
            await client.AuthenticateAsync(smtp.UserName, smtp.Password ?? string.Empty, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(quit: true, ct);
    }

    private static string WrapInLayout(string body, string shopName) =>
        $"""
         <!doctype html>
         <html lang="pl">
         <body style="margin:0;padding:24px;background:#f4f4f5;font-family:Segoe UI,Arial,sans-serif;color:#18181b">
           <div style="max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:32px;line-height:1.5">
             {body}
             <hr style="border:none;border-top:1px solid #e4e4e7;margin:24px 0">
             <p style="font-size:12px;color:#71717a">{System.Net.WebUtility.HtmlEncode(shopName)} - wiadomość wysłana automatycznie, prosimy na nią nie odpowiadać.</p>
           </div>
         </body>
         </html>
         """;
}
