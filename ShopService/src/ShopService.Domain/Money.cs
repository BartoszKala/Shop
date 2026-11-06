using System.Globalization;
using ShopService.Domain.Common;

namespace ShopService.Domain;

/// <summary>
/// Kwota w walucie sklepu (PLN): nieujemna, z dokładnością do grosza. Sklep działa w jednej walucie, więc waluta
/// nie jest zapisywana w bazie – przy wprowadzeniu kolejnej trzeba dodać ją do typu i kolumn.
/// </summary>
public readonly record struct Money : IComparable<Money>
{
    public const string Currency = "PLN";

    public static readonly Money Zero = new(0m);

    private Money(decimal amount) => Amount = amount;

    public decimal Amount { get; }

    /// <summary>Kwota z danych od użytkownika – błąd walidacji zamiast wyjątku.</summary>
    public static Result<Money> Create(decimal amount, string field = nameof(Amount))
    {
        if (amount < 0)
        {
            return Error.Validation(field, "Kwota nie może być ujemna.");
        }
        if (decimal.Round(amount, 2) != amount)
        {
            return Error.Validation(field, "Kwota może mieć najwyżej dwa miejsca po przecinku.");
        }

        return new Money(amount);
    }

    /// <summary>Kwota z zaufanego źródła (baza, obliczenia) – niepoprawna wartość to błąd programisty.</summary>
    public static Money From(decimal amount)
    {
        var money = Create(amount);
        return money.IsSuccess ? money.Value : throw new ArgumentOutOfRangeException(nameof(amount), amount, money.Error.Message);
    }

    public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);

    public static Money operator *(Money price, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        return new Money(price.Amount * quantity);
    }

    public static bool operator <(Money left, Money right) => left.Amount < right.Amount;
    public static bool operator >(Money left, Money right) => left.Amount > right.Amount;
    public static bool operator <=(Money left, Money right) => left.Amount <= right.Amount;
    public static bool operator >=(Money left, Money right) => left.Amount >= right.Amount;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
}

public static class MoneyExtensions
{
    public static Money Sum(this IEnumerable<Money> amounts) => amounts.Aggregate(Money.Zero, (total, amount) => total + amount);
}
