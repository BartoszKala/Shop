using IdentityService.Domain;
using IdentityService.Domain.Common;
using IdentityService.Domain.Constants;
using IdentityService.Domain.Entities;

namespace IdentityService.UnitTests.Domain;

public sealed class ApplicationUserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_email_as_user_name_and_trims_names()
    {
        var result = ApplicationUser.Create(" anna@shop.local ", "  Anna ", " ", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal("anna@shop.local", result.Value.Email);
        Assert.Equal("anna@shop.local", result.Value.UserName);
        Assert.Equal("Anna", result.Value.FirstName);
        Assert.Null(result.Value.LastName);
        Assert.Equal(Now, result.Value.CreatedAt);
        Assert.NotEqual(default, result.Value.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_missing_email(string email)
    {
        var result = ApplicationUser.Create(email, null, null, Now);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.True(result.Error.Details!.ContainsKey(nameof(ApplicationUser.Email)));
    }

    [Fact]
    public void ChangeName_rejects_too_long_name()
    {
        var user = ApplicationUser.Create("a@shop.local", null, null, Now).Value;

        var result = user.ChangeName(new string('x', ApplicationUser.NameMaxLength + 1), null);

        Assert.False(result.IsSuccess);
        Assert.Null(user.FirstName);
    }

    [Fact]
    public void FullName_joins_first_and_last_name()
    {
        var user = ApplicationUser.Create("a@shop.local", "Jan", "Kowalski", Now).Value;

        Assert.Equal("Jan Kowalski", user.FullName);
    }
}

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken(TimeSpan? lifetime = null) =>
        RefreshToken.Create(UserId.New(), SessionId.New(), "hash", Now, lifetime ?? TimeSpan.FromDays(14), "127.0.0.1", "ua").Value;

    [Fact]
    public void Create_rejects_non_positive_lifetime()
    {
        var result = RefreshToken.Create(UserId.New(), SessionId.New(), "hash", Now, TimeSpan.Zero, null, null);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_truncates_long_user_agent()
    {
        var token = RefreshToken.Create(UserId.New(), SessionId.New(), "hash", Now, TimeSpan.FromDays(1), null,
            new string('a', 2000)).Value;

        Assert.Equal(RefreshToken.UserAgentMaxLength, token.UserAgent!.Length);
    }

    [Fact]
    public void Rotate_revokes_and_links_to_successor()
    {
        var token = NewToken();

        var result = token.Rotate(Now.AddHours(1), "next");

        Assert.True(result.IsSuccess);
        Assert.True(token.IsRevoked);
        Assert.True(token.WasRotated);
        Assert.Equal("next", token.ReplacedByTokenHash);
    }

    [Fact]
    public void Rotate_fails_for_already_rotated_token()
    {
        var token = NewToken();
        token.Rotate(Now, "next");

        var result = token.Rotate(Now, "another");

        Assert.Equal("token.revoked", result.Error!.Code);
    }

    [Fact]
    public void Rotate_fails_for_expired_token()
    {
        var token = NewToken(TimeSpan.FromMinutes(1));

        var result = token.Rotate(Now.AddMinutes(2), "next");

        Assert.Equal("token.expired", result.Error!.Code);
        Assert.False(token.IsRevoked);
    }
}

public sealed class UserRoleTests
{
    [Fact]
    public void Create_accepts_known_roles()
    {
        foreach (var role in AppRoles.All)
        {
            Assert.True(UserRole.Create(UserId.New(), role).IsSuccess);
        }
    }

    [Fact]
    public void Create_rejects_unknown_role()
    {
        Assert.False(UserRole.Create(UserId.New(), "SuperAdmin").IsSuccess);
    }
}

public sealed class UserConsentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ConsentType.TermsOfService)]
    [InlineData(ConsentType.PrivacyPolicy)]
    public void Legal_documents_require_version_and_acceptance(ConsentType type)
    {
        Assert.False(UserConsent.Create(UserId.New(), type, true, null, Now, null).IsSuccess);
        Assert.False(UserConsent.Create(UserId.New(), type, false, "v1", Now, null).IsSuccess);
        Assert.True(UserConsent.Create(UserId.New(), type, true, "v1", Now, null).IsSuccess);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Marketing_consent_can_be_granted_or_withdrawn(bool granted)
    {
        var consent = UserConsent.Create(UserId.New(), ConsentType.Marketing, granted, null, Now, "10.0.0.1");

        Assert.True(consent.IsSuccess);
        Assert.Equal(granted, consent.Value.Granted);
    }
}

public sealed class StronglyTypedIdTests
{
    [Fact]
    public void Ids_compare_by_value()
    {
        var guid = Guid.CreateVersion7();

        Assert.Equal(UserId.From(guid), UserId.From(guid));
        Assert.Equal(guid.ToString(), UserId.From(guid).ToString());
    }

    [Fact]
    public void TypeConverter_round_trips_string()
    {
        var id = UserId.New();
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(UserId));

        Assert.Equal(id, converter.ConvertFromInvariantString(id.ToString()));
    }

    [Fact]
    public void New_ids_are_time_ordered()
    {
        var first = UserId.New();
        Thread.Sleep(2);
        var second = UserId.New();

        Assert.True(string.CompareOrdinal(first.ToString(), second.ToString()) < 0);
    }
}
