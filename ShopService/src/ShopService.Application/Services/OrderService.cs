using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Application.Services;

public sealed class OrderService(
    IOrderRepository orders,
    IProductRepository products,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Składa zamówienie. Z kluczem idempotencji ponowne wysłanie (podwójne kliknięcie, zerwane połączenie)
    /// zwraca istniejące zamówienie zamiast tworzyć drugie.
    /// </summary>
    public async Task<Result<PlacedOrder>> PlaceAsync(PlaceOrderRequest request, string? idempotencyKey, CancellationToken ct)
    {
        var quantities = request.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (idempotencyKey is not null && await FindReplayAsync(idempotencyKey, quantities, ct) is { } replay)
        {
            return replay;
        }

        var found = await products.FindManyAsync(quantities.Keys, ct);
        if (found.Count != quantities.Count)
        {
            return OrderErrors.ProductUnavailable;
        }

        var lines = found.Select(p => new OrderLine(p, quantities[p.Id])).ToList();
        var placed = Order.Place(currentUser.UserId, currentUser.Email, request.ShippingAddress.ToDomain(), lines,
            idempotencyKey, timeProvider.GetUtcNow());
        if (!placed.IsSuccess)
        {
            return placed.Error;
        }

        orders.Add(placed.Value);
        var saved = await unitOfWork.SaveChangesAsync(ct);
        if (saved.IsSuccess)
        {
            return new PlacedOrder(OrderResponse.From(placed.Value), IsReplay: false);
        }

        // Równoległe żądanie z tym samym kluczem mogło wygrać wyścig (unikalny indeks albo konflikt stanu magazynu)
        if (idempotencyKey is not null && await FindReplayAsync(idempotencyKey, quantities, ct) is { } concurrentReplay)
        {
            return concurrentReplay;
        }

        return saved.Error;
    }

    public Task<PagedResponse<OrderSummary>> GetMineAsync(PageRequest page, CancellationToken ct) =>
        orders.SearchAsync(currentUser.UserId, null, page, ct);

    public async Task<Result<OrderResponse>> GetMineAsync(OrderId id, CancellationToken ct) =>
        await orders.FindAsync(id, ct) is { } order && order.UserId == currentUser.UserId
            ? OrderResponse.From(order)
            : OrderErrors.NotFound;

    public Task<PagedResponse<OrderSummary>> SearchAsync(OrderSearchRequest request, CancellationToken ct) =>
        orders.SearchAsync(null, request.Status, request, ct);

    public async Task<Result<OrderResponse>> GetAsync(OrderId id, CancellationToken ct) =>
        await orders.FindAsync(id, ct) is { } order ? OrderResponse.From(order) : OrderErrors.NotFound;

    public Task<Result<OrderResponse>> MarkAsPaidAsync(OrderId id, CancellationToken ct) =>
        ExecuteAsync(id, (order, now) => order.MarkAsPaid(now), ct);

    public Task<Result<OrderResponse>> ShipAsync(OrderId id, CancellationToken ct) =>
        ExecuteAsync(id, (order, now) => order.Ship(now), ct);

    public Task<Result<OrderResponse>> MarkAsDeliveredAsync(OrderId id, CancellationToken ct) =>
        ExecuteAsync(id, (order, now) => order.MarkAsDelivered(now), ct);

    /// <summary>Towar wraca do magazynu w handlerze zdarzenia OrderCancelled, w tej samej transakcji.</summary>
    public Task<Result<OrderResponse>> CancelAsync(OrderId id, CancellationToken ct) =>
        ExecuteAsync(id, (order, now) => order.Cancel(now), ct);

    private async Task<Result<OrderResponse>> ExecuteAsync(OrderId id, Func<Order, DateTimeOffset, Result> action, CancellationToken ct)
    {
        var order = await orders.FindAsync(id, ct);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var result = action(order, timeProvider.GetUtcNow());
        if (!result.IsSuccess)
        {
            return result.Error;
        }

        var saved = await unitOfWork.SaveChangesAsync(ct);
        return saved.IsSuccess ? OrderResponse.From(order) : saved.Error;
    }

    private async Task<Result<PlacedOrder>?> FindReplayAsync(
        string idempotencyKey, IReadOnlyDictionary<ProductId, int> quantities, CancellationToken ct)
    {
        var existing = await orders.FindByIdempotencyKeyAsync(currentUser.UserId, idempotencyKey, ct);
        if (existing is null)
        {
            return null;
        }

        return existing.HasSameLines(quantities)
            ? new PlacedOrder(OrderResponse.From(existing), IsReplay: true)
            : (Result<PlacedOrder>)OrderErrors.IdempotencyKeyReused;
    }
}

/// <summary><see cref="IsReplay"/> = true, gdy zwrócono zamówienie złożone wcześniej z tym samym kluczem idempotencji.</summary>
public sealed record PlacedOrder(OrderResponse Order, bool IsReplay);
