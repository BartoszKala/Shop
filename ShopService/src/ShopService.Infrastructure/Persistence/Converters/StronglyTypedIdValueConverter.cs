using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShopService.Domain.Common;

namespace ShopService.Infrastructure.Persistence.Converters;

internal sealed class StronglyTypedIdValueConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, value => Create(value))
    where TId : struct, IStronglyTypedId<TId>
{
    private static TId Create(Guid value) => TId.From(value);
}
