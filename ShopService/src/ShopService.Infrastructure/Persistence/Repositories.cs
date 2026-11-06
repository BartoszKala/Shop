using Microsoft.EntityFrameworkCore;
using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Infrastructure.Persistence;


internal sealed class CategoryRepository(ShopDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct = default) =>
        await Project(db.Categories).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<CategoryResponse?> GetAsync(CategoryId id, CancellationToken ct = default) =>
        Project(db.Categories.Where(c => c.Id == id)).SingleOrDefaultAsync(ct);

    public Task<Category?> FindAsync(CategoryId id, CancellationToken ct = default) =>
        db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(CategoryId id, CancellationToken ct = default) =>
        db.Categories.AnyAsync(c => c.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CategoryId? exceptId, CancellationToken ct = default) =>
        db.Categories.AnyAsync(c => c.Slug == slug && c.Id != exceptId, ct);

    public Task<bool> HasProductsAsync(CategoryId id, CancellationToken ct = default) =>
        db.Products.AnyAsync(p => p.CategoryId == id, ct);

    public void Add(Category category) => db.Categories.Add(category);

    public void Remove(Category category) => db.Categories.Remove(category);

    private IQueryable<CategoryResponse> Project(IQueryable<Category> query) =>
        query.Select(c => new CategoryResponse(c.Id, c.Name, c.Slug, c.Description,
            db.Products.Count(p => p.CategoryId == c.Id && p.IsActive)));
}

internal sealed class ProductRepository(ShopDbContext db) : IProductRepository
{
    public async Task<PagedResponse<ProductListItem>> SearchAsync(
        ProductSearchRequest request, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{EscapeLike(request.Search.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern, @"\")
                                     || (p.Description != null && EF.Functions.ILike(p.Description, pattern, @"\")));
        }

        query = request.Sort switch
        {
            ProductSort.Name => query.OrderBy(p => p.Name).ThenBy(p => p.Id),
            ProductSort.PriceAsc => query.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ProductSort.PriceDesc => query.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(p => new ProductListItem(p.Id, p.Name, p.Price.Amount, p.ImageUrl, p.CategoryId, p.Category.Name, p.Stock, p.IsActive))
            .ToListAsync(ct);

        return new PagedResponse<ProductListItem>(items, request.Page, request.PageSize, total);
    }

    public Task<ProductResponse?> GetAsync(ProductId id, bool includeInactive, CancellationToken ct = default) =>
        db.Products.AsNoTracking()
            .Where(p => p.Id == id && (includeInactive || p.IsActive))
            .Select(p => new ProductResponse(p.Id, p.Name, p.Description, p.Price.Amount, p.Stock, p.ImageUrl, p.IsActive,
                p.CategoryId, p.Category.Name, p.CreatedAt, p.UpdatedAt))
            .SingleOrDefaultAsync(ct);

    public Task<Product?> FindAsync(ProductId id, CancellationToken ct = default) =>
        db.Products.SingleOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> FindManyAsync(IReadOnlyCollection<ProductId> ids, CancellationToken ct = default) =>
        await db.Products.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public Task<bool> IsUsedInOrdersAsync(ProductId id, CancellationToken ct = default) =>
        db.OrderItems.AnyAsync(i => i.ProductId == id, ct);

    public void Add(Product product) => db.Products.Add(product);

    public void Remove(Product product) => db.Products.Remove(product);

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

internal sealed class OrderRepository(ShopDbContext db) : IOrderRepository
{
    public Task<Order?> FindAsync(OrderId id, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order?> FindByIdempotencyKeyAsync(UserId userId, string idempotencyKey, CancellationToken ct = default) =>
        db.Orders.AsNoTracking().Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.UserId == userId && o.IdempotencyKey == idempotencyKey, ct);

    public async Task<PagedResponse<OrderSummary>> SearchAsync(
        UserId? userId, OrderStatus? status, PageRequest page, CancellationToken ct = default)
    {
        var query = db.Orders.AsNoTracking();
        if (userId is { } user)
        {
            query = query.Where(o => o.UserId == user);
        }
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(o => new OrderSummary(o.Id, o.UserId, o.CustomerEmail, o.Status, o.Total.Amount,
                o.Items.Sum(i => i.Quantity), o.CreatedAt, o.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResponse<OrderSummary>(items, page.Page, page.PageSize, total);
    }

    public void Add(Order order) => db.Orders.Add(order);
}
