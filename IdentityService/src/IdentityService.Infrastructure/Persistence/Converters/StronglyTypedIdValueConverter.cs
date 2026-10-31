using IdentityService.Domain.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IdentityService.Infrastructure.Persistence.Converters;

internal sealed class StronglyTypedIdValueConverter<TId>() : ValueConverter<TId, Guid>(id => id.Value, value => Create(value))
    where TId : struct, IStronglyTypedId<TId>
{
    private static TId Create(Guid value) => TId.From(value);
}
