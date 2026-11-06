using System.ComponentModel.DataAnnotations;
using ShopService.Domain;
using ShopService.Domain.Entities;

namespace ShopService.Application.Contracts;

public sealed record CategoryResponse(CategoryId Id, string Name, string Slug, string? Description, int ProductCount);

public sealed record CategoryRequest
{
    [Required, MaxLength(Category.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    /// <summary>Opcjonalny – gdy pusty, powstaje z nazwy (np. „Dom i ogród” → <c>dom-i-ogrod</c>).</summary>
    [MaxLength(Domain.Common.Slug.MaxLength), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Adres może zawierać tylko małe litery, cyfry i myślniki.")]
    public string? Slug { get; init; }

    [MaxLength(Category.DescriptionMaxLength)]
    public string? Description { get; init; }
}

public enum ProductSort
{
    Newest,
    Name,
    PriceAsc,
    PriceDesc
}

public sealed record ProductSearchRequest : PageRequest
{
    public CategoryId? CategoryId { get; init; }

    [MaxLength(100)]
    public string? Search { get; init; }

    public ProductSort Sort { get; init; } = ProductSort.Newest;
}

public sealed record ProductListItem(
    ProductId Id,
    string Name,
    decimal Price,
    string? ImageUrl,
    CategoryId CategoryId,
    string CategoryName,
    int Stock,
    bool IsActive);

public sealed record ProductResponse(
    ProductId Id,
    string Name,
    string? Description,
    decimal Price,
    int Stock,
    string? ImageUrl,
    bool IsActive,
    CategoryId CategoryId,
    string CategoryName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ProductRequest
{
    [Required]
    public CategoryId CategoryId { get; init; }

    [Required, MaxLength(Product.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(Product.DescriptionMaxLength)]
    public string? Description { get; init; }

    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Price { get; init; }

    [Range(0, 1_000_000)]
    public int Stock { get; init; }

    [Url, MaxLength(Product.ImageUrlMaxLength)]
    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; } = true;
}
