using IdentityService.Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace IdentityService.Domain.Entities;

public class ApplicationUser : IdentityUser<UserId>
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;

    private ApplicationUser()
    {
    }

    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }

    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];
    public ICollection<UserRole> Roles { get; private set; } = [];
    public ICollection<UserConsent> Consents { get; private set; } = [];

    public string? FullName
    {
        get
        {
            var name = $"{FirstName} {LastName}".Trim();
            return name.Length == 0 ? null : name;
        }
    }

    public static Result<ApplicationUser> Create(string email, string? firstName, string? lastName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Error.Validation(nameof(Email), "Adres e-mail jest wymagany.");
        }
        if (email.Length > EmailMaxLength)
        {
            return Error.Validation(nameof(Email), $"Adres e-mail może mieć najwyżej {EmailMaxLength} znaków.");
        }

        var user = new ApplicationUser
        {
            Id = UserId.New(),
            UserName = email.Trim(),
            Email = email.Trim(),
            CreatedAt = now,
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var named = user.ChangeName(firstName, lastName);
        return named.IsSuccess ? user : named.Error;
    }

    public Result ChangeName(string? firstName, string? lastName)
    {
        if (firstName?.Length > NameMaxLength)
        {
            return Error.Validation(nameof(FirstName), $"Imię może mieć najwyżej {NameMaxLength} znaków.");
        }
        if (lastName?.Length > NameMaxLength)
        {
            return Error.Validation(nameof(LastName), $"Nazwisko może mieć najwyżej {NameMaxLength} znaków.");
        }

        FirstName = Normalize(firstName);
        LastName = Normalize(lastName);
        return Result.Success();
    }

    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
