using ShopService.Domain.Common;

namespace ShopService.Domain.Entities;

public class Category
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    private Category()
    {
    }

    public CategoryId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public static Result<Category> Create(string name, string? slug, string? description)
    {
        var category = new Category { Id = CategoryId.New() };
        var updated = category.Update(name, slug, description);
        return updated.IsSuccess ? category : updated.Error;
    }

    public Result Update(string name, string? slug, string? description)
    {
        name = name.Trim();
        if (name.Length is 0 or > NameMaxLength)
        {
            return Error.Validation(nameof(Name), $"Nazwa musi mieć od 1 do {NameMaxLength} znaków.");
        }

        slug = string.IsNullOrWhiteSpace(slug) ? Common.Slug.From(name) : slug.Trim();
        if (!Common.Slug.IsValid(slug))
        {
            return Error.Validation(nameof(Slug), "Adres może zawierać tylko małe litery, cyfry i myślniki.");
        }

        description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (description is { Length: > DescriptionMaxLength })
        {
            return Error.Validation(nameof(Description), $"Opis może mieć najwyżej {DescriptionMaxLength} znaków.");
        }

        Name = name;
        Slug = slug;
        Description = description;
        return Result.Success();
    }
}
