using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Application.Services;

public sealed class CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork)
{
    public Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct) => categories.GetAllAsync(ct);

    public async Task<Result<CategoryResponse>> GetAsync(CategoryId id, CancellationToken ct) =>
        await categories.GetAsync(id, ct) is { } category ? category : CategoryErrors.NotFound;

    public async Task<Result<CategoryResponse>> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        var created = Category.Create(request.Name, request.Slug, request.Description);
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        var category = created.Value;
        if (await categories.SlugExistsAsync(category.Slug, null, ct))
        {
            return CategoryErrors.SlugTaken;
        }

        categories.Add(category);
        return await SaveAndGetAsync(category.Id, ct);
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(CategoryId id, CategoryRequest request, CancellationToken ct)
    {
        var category = await categories.FindAsync(id, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        var updated = category.Update(request.Name, request.Slug, request.Description);
        if (!updated.IsSuccess)
        {
            return updated.Error;
        }
        if (await categories.SlugExistsAsync(category.Slug, category.Id, ct))
        {
            return CategoryErrors.SlugTaken;
        }

        return await SaveAndGetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(CategoryId id, CancellationToken ct)
    {
        var category = await categories.FindAsync(id, ct);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }
        if (await categories.HasProductsAsync(id, ct))
        {
            return CategoryErrors.HasProducts;
        }

        categories.Remove(category);
        return await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Result<CategoryResponse>> SaveAndGetAsync(CategoryId id, CancellationToken ct)
    {
        var saved = await unitOfWork.SaveChangesAsync(ct);
        if (!saved.IsSuccess)
        {
            return saved.Error;
        }

        return (await categories.GetAsync(id, ct))!;
    }
}
