using ShopService.Application.Contracts;
using ShopService.Contracts;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Application.Abstractions;

public interface ICurrentUser
{
    UserId UserId { get; }

    string Email { get; }
}

public interface IUnitOfWork
{
    /// <summary>
    /// Rozsyła zdarzenia domenowe do handlerów i zapisuje wszystko w jednej transakcji. Konflikt współbieżności
    /// (np. równoległe zamówienie ostatniej sztuki) i naruszenie unikalności zwraca jako błąd.
    /// </summary>
    Task<Result> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Reakcja na zdarzenie domenowe w tej samej transakcji co zmiana, która je wywołała.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}

/// <summary>Zapisuje zdarzenie integracyjne w outboxie; zostanie wysłane do RabbitMQ po zatwierdzeniu transakcji.</summary>
public interface IIntegrationEventPublisher
{
    void Publish<TEvent>(TEvent integrationEvent) where TEvent : IIntegrationEvent;
}

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct = default);

    Task<CategoryResponse?> GetAsync(CategoryId id, CancellationToken ct = default);

    Task<Category?> FindAsync(CategoryId id, CancellationToken ct = default);

    Task<bool> ExistsAsync(CategoryId id, CancellationToken ct = default);

    Task<bool> SlugExistsAsync(string slug, CategoryId? exceptId, CancellationToken ct = default);

    Task<bool> HasProductsAsync(CategoryId id, CancellationToken ct = default);

    void Add(Category category);

    void Remove(Category category);
}

public interface IProductRepository
{
    Task<PagedResponse<ProductListItem>> SearchAsync(ProductSearchRequest request, bool includeInactive, CancellationToken ct = default);

    Task<ProductResponse?> GetAsync(ProductId id, bool includeInactive, CancellationToken ct = default);

    Task<Product?> FindAsync(ProductId id, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> FindManyAsync(IReadOnlyCollection<ProductId> ids, CancellationToken ct = default);

    Task<bool> IsUsedInOrdersAsync(ProductId id, CancellationToken ct = default);

    void Add(Product product);

    void Remove(Product product);
}

public interface IOrderRepository
{
    Task<Order?> FindAsync(OrderId id, CancellationToken ct = default);

    /// <summary>Czyta z bazy z pominięciem śledzonych encji – używane także po nieudanym zapisie równoległego żądania.</summary>
    Task<Order?> FindByIdempotencyKeyAsync(UserId userId, string idempotencyKey, CancellationToken ct = default);

    Task<PagedResponse<OrderSummary>> SearchAsync(UserId? userId, OrderStatus? status, PageRequest page, CancellationToken ct = default);

    void Add(Order order);
}
