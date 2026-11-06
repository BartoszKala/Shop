using ShopService.Domain.Common;

namespace ShopService.Domain.Entities;

public class Product
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int ImageUrlMaxLength = 500;
    public static readonly Money MaxPrice = Money.From(1_000_000m);

    private Product()
    {
    }

    public ProductId Id { get; private set; }

    public CategoryId CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;

    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Price { get; private set; }
    public int Stock { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Token współbieżności (xmin w PostgreSQL) - chroni stan magazynu przy równoległych zamówieniach.</summary>
    public uint Version { get; private set; }

    public static Result<Product> Create(
        CategoryId categoryId, string name, string? description, Money price, int stock, string? imageUrl, bool isActive, DateTimeOffset now)
    {
        var product = new Product { Id = ProductId.New(), CreatedAt = now };
        var updated = product.Update(categoryId, name, description, price, stock, imageUrl, isActive, now);
        return updated.IsSuccess ? product : updated.Error;
    }

    public Result Update(
        CategoryId categoryId, string name, string? description, Money price, int stock, string? imageUrl, bool isActive, DateTimeOffset now)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            return Error.Validation(nameof(Name), $"Nazwa musi mieć od 1 do {NameMaxLength} znaków.");
        }

        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (description is { Length: > DescriptionMaxLength })
        {
            return Error.Validation(nameof(Description), $"Opis może mieć najwyżej {DescriptionMaxLength} znaków.");
        }
        if (price == Money.Zero || price > MaxPrice)
        {
            return Error.Validation(nameof(Price), "Cena musi być większa od zera i nie większa niż 1 000 000 zł.");
        }
        if (stock < 0)
        {
            return Error.Validation(nameof(Stock), "Stan magazynowy nie może być ujemny.");
        }

        imageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
        if (imageUrl is { Length: > ImageUrlMaxLength })
        {
            return Error.Validation(nameof(ImageUrl), $"Adres obrazka może mieć najwyżej {ImageUrlMaxLength} znaków.");
        }

        CategoryId = categoryId;
        Name = name;
        Description = description;
        Price = price;
        Stock = stock;
        ImageUrl = imageUrl;
        IsActive = isActive;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result RemoveFromStock(int quantity, DateTimeOffset now)
    {
        if (quantity <= 0)
        {
            return Error.Validation("Quantity", "Ilość musi być dodatnia.");
        }
        if (quantity > Stock)
        {
            return Error.Conflict("product.insufficient_stock", $"Niewystarczająca ilość produktu „{Name}” w magazynie (dostępne: {Stock}).");
        }

        Stock -= quantity;
        UpdatedAt = now;
        return Result.Success();
    }

    public void ReturnToStock(int quantity, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        Stock += quantity;
        UpdatedAt = now;
    }
}
