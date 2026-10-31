using System.ComponentModel;
using IdentityService.Domain.Common;

namespace IdentityService.Domain;

[TypeConverter(typeof(StronglyTypedIdTypeConverter<UserId>))]
public readonly record struct UserId : IStronglyTypedId<UserId>
{
    private UserId(Guid value) => Value = value;

    public Guid Value { get; }

    public static UserId New() => new(Guid.CreateVersion7());
    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<RefreshTokenId>))]
public readonly record struct RefreshTokenId : IStronglyTypedId<RefreshTokenId>
{
    private RefreshTokenId(Guid value) => Value = value;

    public Guid Value { get; }

    public static RefreshTokenId New() => new(Guid.CreateVersion7());
    public static RefreshTokenId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<SessionId>))]
public readonly record struct SessionId : IStronglyTypedId<SessionId>
{
    private SessionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SessionId New() => new(Guid.CreateVersion7());
    public static SessionId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<SecurityEventId>))]
public readonly record struct SecurityEventId : IStronglyTypedId<SecurityEventId>
{
    private SecurityEventId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SecurityEventId New() => new(Guid.CreateVersion7());
    public static SecurityEventId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<UserConsentId>))]
public readonly record struct UserConsentId : IStronglyTypedId<UserConsentId>
{
    private UserConsentId(Guid value) => Value = value;

    public Guid Value { get; }

    public static UserConsentId New() => new(Guid.CreateVersion7());
    public static UserConsentId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
