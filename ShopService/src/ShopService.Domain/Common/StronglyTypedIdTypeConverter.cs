using System.ComponentModel;
using System.Globalization;

namespace ShopService.Domain.Common;

public sealed class StronglyTypedIdTypeConverter<TId> : TypeConverter
    where TId : struct, IStronglyTypedId<TId>
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || sourceType == typeof(Guid) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || destinationType == typeof(Guid) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        string text => TId.From(Guid.Parse(text)),
        Guid guid => TId.From(guid),
        _ => base.ConvertFrom(context, culture, value)
    };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        value is TId id && destinationType == typeof(string) ? id.Value.ToString()
        : value is TId guidId && destinationType == typeof(Guid) ? guidId.Value
        : base.ConvertTo(context, culture, value, destinationType);
}
