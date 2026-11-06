using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ShopService.Application.Abstractions;
using ShopService.Infrastructure.Outbox;
using ShopService.Infrastructure.Persistence;

namespace ShopService.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "ShopDb";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>().BindConfiguration(OutboxOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<MessagingOptions>().BindConfiguration(MessagingOptions.SectionName);

        services.AddDbContext<ShopDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString(DatabaseConnectionName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();

        AddOutbox(services);

        return services;
    }

    private static void AddOutbox(IServiceCollection services)
    {
        services.AddScoped<IIntegrationEventPublisher, OutboxIntegrationEventPublisher>();

        services.AddSingleton<IIntegrationEventSender>(sp =>
            sp.GetRequiredService<IOptions<MessagingOptions>>().Value.Enabled
                ? ActivatorUtilities.CreateInstance<RabbitMqIntegrationEventSender>(sp, sp.GetRequiredService<IConnection>())
                : ActivatorUtilities.CreateInstance<LoggingIntegrationEventSender>(sp));

        services.AddHostedService<OutboxProcessor>();
    }
}
