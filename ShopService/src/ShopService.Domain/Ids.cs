using System.ComponentModel;
using ShopService.Domain.Common;

namespace ShopService.Domain;

[TypeConverter(typeof(StronglyTypedIdTypeConverter<CategoryId>))]
public readonly record struct CategoryId : IStronglyTypedId<CategoryId>
{
    private CategoryId(Guid value) => Value = value;

    public Guid Value { get; }

    public static CategoryId New() => new(Guid.CreateVersion7());
    public static CategoryId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<ProductId>))]
public readonly record struct ProductId : IStronglyTypedId<ProductId>
{
    private ProductId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ProductId New() => new(Guid.CreateVersion7());
    public static ProductId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<OrderId>))]
public readonly record struct OrderId : IStronglyTypedId<OrderId>
{
    private OrderId(Guid value) => Value = value;

    public Guid Value { get; }

    public static OrderId New() => new(Guid.CreateVersion7());
    public static OrderId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

[TypeConverter(typeof(StronglyTypedIdTypeConverter<OrderItemId>))]
public readonly record struct OrderItemId : IStronglyTypedId<OrderItemId>
{
    private OrderItemId(Guid value) => Value = value;

    public Guid Value { get; }

    public static OrderItemId New() => new(Guid.CreateVersion7());
    public static OrderItemId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

/// <summary>Id użytkownika z IdentityService (claim <c>sub</c>) – ShopService go nie nadaje, tylko odczytuje.</summary>
[TypeConverter(typeof(StronglyTypedIdTypeConverter<UserId>))]
public readonly record struct UserId : IStronglyTypedId<UserId>
{
    private UserId(Guid value) => Value = value;

    public Guid Value { get; }

    public static UserId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
