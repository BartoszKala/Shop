using ShopService.Application.Abstractions;
using ShopService.Domain;
using ShopService.Domain.Events;
using Integration = ShopService.Contracts;

namespace ShopService.Application.EventHandlers;

/// <summary>Anulowane zamówienie oddaje towar do magazynu – w tej samej transakcji co zmiana statusu.</summary>
internal sealed class ReturnStockWhenOrderCancelled(IProductRepository products) : IDomainEventHandler<OrderCancelled>
{
    public async Task HandleAsync(OrderCancelled domainEvent, CancellationToken ct = default)
    {
        var stock = (await products.FindManyAsync(domainEvent.Lines.Select(l => l.ProductId).ToList(), ct))
            .ToDictionary(p => p.Id);
        foreach (var line in domainEvent.Lines)
        {
            // Produkt mógł zostać w międzyczasie usunięty – wtedy nie ma czego uzupełniać
            if (stock.TryGetValue(line.ProductId, out var product))
            {
                product.ReturnToStock(line.Quantity, domainEvent.OccurredAt);
            }
        }
    }
}

internal sealed class PublishOrderPlaced(IIntegrationEventPublisher publisher) : IDomainEventHandler<OrderPlaced>
{
    public Task HandleAsync(OrderPlaced domainEvent, CancellationToken ct = default)
    {
        publisher.Publish(new Integration.OrderPlaced(
            domainEvent.OrderId.Value,
            domainEvent.UserId.Value,
            domainEvent.CustomerEmail,
            domainEvent.Total.Amount,
            Money.Currency,
            domainEvent.ItemCount,
            domainEvent.OccurredAt));
        return Task.CompletedTask;
    }
}

internal sealed class PublishOrderStatusChanged(IIntegrationEventPublisher publisher) : IDomainEventHandler<OrderStatusChanged>
{
    public Task HandleAsync(OrderStatusChanged domainEvent, CancellationToken ct = default)
    {
        publisher.Publish(new Integration.OrderStatusChanged(
            domainEvent.OrderId.Value,
            domainEvent.UserId.Value,
            domainEvent.CustomerEmail,
            domainEvent.PreviousStatus.ToString(),
            domainEvent.NewStatus.ToString(),
            domainEvent.OccurredAt));
        return Task.CompletedTask;
    }
}
