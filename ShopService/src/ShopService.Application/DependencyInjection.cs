using Microsoft.Extensions.DependencyInjection;
using ShopService.Application.Abstractions;
using ShopService.Application.EventHandlers;
using ShopService.Application.Services;
using ShopService.Domain.Events;

namespace ShopService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CategoryService>();
        services.AddScoped<ProductService>();
        services.AddScoped<OrderService>();

        services.AddScoped<IDomainEventHandler<OrderCancelled>, ReturnStockWhenOrderCancelled>();
        services.AddScoped<IDomainEventHandler<OrderPlaced>, PublishOrderPlaced>();
        services.AddScoped<IDomainEventHandler<OrderStatusChanged>, PublishOrderStatusChanged>();

        return services;
    }
}
