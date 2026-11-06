using ShopService.Domain.Common;

namespace ShopService.Application.Common;

public static class CommonErrors
{
    public static readonly Error ConcurrencyConflict = Error.Conflict("common.concurrency_conflict", "Dane zostały w międzyczasie zmienione. Odśwież i spróbuj ponownie.");
    public static readonly Error Duplicate = Error.Conflict("common.duplicate", "Taki rekord już istnieje.");
}

public static class CategoryErrors
{
    public static readonly Error NotFound = Error.NotFound("category.not_found", "Kategoria nie istnieje.");
    public static readonly Error SlugTaken = Error.Conflict("category.slug_taken", "Kategoria o takim adresie już istnieje.");
    public static readonly Error HasProducts = Error.Conflict("category.has_products", "Nie można usunąć kategorii, która zawiera produkty.");
}

public static class ProductErrors
{
    public static readonly Error NotFound = Error.NotFound("product.not_found", "Produkt nie istnieje.");
    public static readonly Error CategoryNotFound = Error.Validation("CategoryId", "Wybrana kategoria nie istnieje.");
    public static readonly Error UsedInOrders = Error.Conflict("product.used_in_orders", "Produkt występuje w zamówieniach – zamiast usuwać, oznacz go jako nieaktywny.");
}

public static class OrderErrors
{
    public static readonly Error NotFound = Error.NotFound("order.not_found", "Zamówienie nie istnieje.");
    public static readonly Error ProductUnavailable = Error.Conflict("order.product_unavailable", "Co najmniej jeden produkt z koszyka nie jest już dostępny.");
    public static readonly Error IdempotencyKeyReused = Error.Conflict("order.idempotency_key_reused", "Ten klucz idempotencji użyto już dla innego zamówienia.");
}
