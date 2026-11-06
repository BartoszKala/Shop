using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopService.Api.Extensions;
using ShopService.Application.Common;
using ShopService.Application.Contracts;
using ShopService.Application.Services;
using ShopService.Domain;

namespace ShopService.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public sealed class OrdersController(OrderService orderService) : ControllerBase
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    /// <summary>
    /// Składa zamówienie z zawartości koszyka; ceny i dostępność liczone są po stronie serwera.
    /// Z nagłówkiem <c>Idempotency-Key</c> (np. GUID wygenerowany przy otwarciu podsumowania koszyka) ponowne
    /// wysłanie zwraca 200 z tym samym zamówieniem zamiast tworzyć drugie.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Place(
        PlaceOrderRequest request, [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey, CancellationToken ct)
    {
        var result = await orderService.PlaceAsync(request, idempotencyKey, ct);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error);
        }

        var (order, isReplay) = result.Value;
        return isReplay ? Ok(order) : CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    /// <summary>Historia zamówień zalogowanego użytkownika.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderSummary>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<OrderSummary>> GetMine([FromQuery] PageRequest page, CancellationToken ct) =>
        await orderService.GetMineAsync(page, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.GetMineAsync(id, ct));
}

/// <summary>Obsługa zamówień przez administratora: <c>New → Paid → Shipped → Delivered</c>, anulowanie z New lub Paid.</summary>
[ApiController]
[Authorize(Roles = AppRoles.Admin)]
[Route("api/admin/orders")]
[Produces("application/json")]
public sealed class AdminOrdersController(OrderService orderService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<OrderSummary>>(StatusCodes.Status200OK)]
    public async Task<PagedResponse<OrderSummary>> Search([FromQuery] OrderSearchRequest request, CancellationToken ct) =>
        await orderService.SearchAsync(request, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderResponse>> Get(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.GetAsync(id, ct));

    [HttpPost("{id:guid}/mark-paid")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> MarkAsPaid(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.MarkAsPaidAsync(id, ct));

    [HttpPost("{id:guid}/ship")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Ship(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.ShipAsync(id, ct));

    [HttpPost("{id:guid}/mark-delivered")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> MarkAsDelivered(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.MarkAsDeliveredAsync(id, ct));

    /// <summary>Anulowanie zwraca towar do magazynu.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType<OrderResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderResponse>> Cancel(OrderId id, CancellationToken ct) =>
        this.ToActionResult(await orderService.CancelAsync(id, ct));
}
