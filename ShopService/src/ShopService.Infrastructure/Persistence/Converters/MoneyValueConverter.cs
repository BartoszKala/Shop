using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShopService.Domain;

namespace ShopService.Infrastructure.Persistence.Converters;

internal sealed class MoneyValueConverter() : ValueConverter<Money, decimal>(money => money.Amount, amount => Money.From(amount));
