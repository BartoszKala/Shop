using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Application.Services;

public sealed class ProductService(
    IProductRepository products,
    ICategoryRepository categories,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<PagedResponse<ProductListItem>> SearchAsync(ProductSearchRequest request, bool includeInactive, CancellationToken ct) =>
        products.SearchAsync(request, includeInactive, ct);

    public async Task<Result<ProductResponse>> GetAsync(ProductId id, bool includeInactive, CancellationToken ct) =>
        await products.GetAsync(id, includeInactive, ct) is { } product ? product : ProductErrors.NotFound;

    public async Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken ct)
    {
        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            return ProductErrors.CategoryNotFound;
        }

        var price = Money.Create(request.Price, nameof(request.Price));
        if (!price.IsSuccess)
        {
            return price.Error;
        }

        var created = Product.Create(request.CategoryId, request.Name, request.Description, price.Value, request.Stock,
            request.ImageUrl, request.IsActive, timeProvider.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        products.Add(created.Value);
        return await SaveAndGetAsync(created.Value.Id, ct);
    }

    public async Task<Result<ProductResponse>> UpdateAsync(ProductId id, ProductRequest request, CancellationToken ct)
    {
        var product = await products.FindAsync(id, ct);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }
        if (product.CategoryId != request.CategoryId && !await categories.ExistsAsync(request.CategoryId, ct))
        {
            return ProductErrors.CategoryNotFound;
        }

        var price = Money.Create(request.Price, nameof(request.Price));
        if (!price.IsSuccess)
        {
            return price.Error;
        }

        var updated = product.Update(request.CategoryId, request.Name, request.Description, price.Value, request.Stock,
            request.ImageUrl, request.IsActive, timeProvider.GetUtcNow());
        if (!updated.IsSuccess)
        {
            return updated.Error;
        }

        return await SaveAndGetAsync(id, ct);
    }

    public async Task<Result> DeleteAsync(ProductId id, CancellationToken ct)
    {
        var product = await products.FindAsync(id, ct);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }
        if (await products.IsUsedInOrdersAsync(id, ct))
        {
            return ProductErrors.UsedInOrders;
        }

        products.Remove(product);
        return await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task<Result<ProductResponse>> SaveAndGetAsync(ProductId id, CancellationToken ct)
    {
        var saved = await unitOfWork.SaveChangesAsync(ct);
        if (!saved.IsSuccess)
        {
            return saved.Error;
        }

        return (await products.GetAsync(id, includeInactive: true, ct))!;
    }
}
