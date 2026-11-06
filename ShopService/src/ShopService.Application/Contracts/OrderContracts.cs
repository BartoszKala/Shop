using System.ComponentModel.DataAnnotations;
using ShopService.Domain;
using ShopService.Domain.Entities;

namespace ShopService.Application.Contracts;

public sealed record OrderLineRequest
{
    [Required]
    public ProductId ProductId { get; init; }

    [Range(1, Order.MaxQuantityPerLine)]
    public int Quantity { get; init; }
}

public sealed record ShippingAddressRequest
{
    [Required, MaxLength(ShippingAddress.RecipientMaxLength)]
    public string Recipient { get; init; } = string.Empty;

    [Required, MaxLength(ShippingAddress.StreetMaxLength)]
    public string Street { get; init; } = string.Empty;

    [Required, RegularExpression(@"^\d{2}-\d{3}$", ErrorMessage = "Kod pocztowy musi mieć format 00-000.")]
    public string PostalCode { get; init; } = string.Empty;

    [Required, MaxLength(ShippingAddress.CityMaxLength)]
    public string City { get; init; } = string.Empty;

    [Required, Phone, MaxLength(ShippingAddress.PhoneMaxLength)]
    public string Phone { get; init; } = string.Empty;

    public ShippingAddress ToDomain() => new(Recipient.Trim(), Street.Trim(), PostalCode.Trim(), City.Trim(), Phone.Trim());
}

/// <summary>Koszyk trzyma front-end; przy składaniu zamówienia ceny i dostępność są liczone od nowa po stronie serwera.</summary>
public sealed record PlaceOrderRequest
{
    [Required, MinLength(1), MaxLength(Order.MaxLines)]
    public IReadOnlyList<OrderLineRequest> Items { get; init; } = [];

    [Required]
    public ShippingAddressRequest ShippingAddress { get; init; } = null!;
}

public sealed record OrderSearchRequest : PageRequest
{
    public OrderStatus? Status { get; init; }
}

public sealed record OrderSummary(
    OrderId Id,
    UserId UserId,
    string CustomerEmail,
    OrderStatus Status,
    decimal Total,
    int ItemCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record OrderItemResponse(ProductId ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed record OrderResponse(
    OrderId Id,
    UserId UserId,
    string CustomerEmail,
    OrderStatus Status,
    IReadOnlyList<OrderStatus> AllowedNextStatuses,
    decimal Total,
    ShippingAddress ShippingAddress,
    IReadOnlyList<OrderItemResponse> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static OrderResponse From(Order order) => new(
        order.Id,
        order.UserId,
        order.CustomerEmail,
        order.Status,
        Enum.GetValues<OrderStatus>().Where(order.CanChangeStatusTo).ToList(),
        order.Total.Amount,
        order.ShippingAddress,
        order.Items.Select(i => new OrderItemResponse(i.ProductId, i.ProductName, i.UnitPrice.Amount, i.Quantity, i.LineTotal.Amount)).ToList(),
        order.CreatedAt,
        order.UpdatedAt);
}
