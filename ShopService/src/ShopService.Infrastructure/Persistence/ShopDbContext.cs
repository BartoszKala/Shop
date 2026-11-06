using Microsoft.EntityFrameworkCore;
using ShopService.Domain;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;
using ShopService.Infrastructure.Outbox;
using ShopService.Infrastructure.Persistence.Converters;

namespace ShopService.Infrastructure.Persistence;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(ShopDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        var idTypes = typeof(ProductId).Assembly.GetTypes()
            .Where(StronglyTypedIds.IsStronglyTypedId);

        foreach (var idType in idTypes)
        {
            configurationBuilder.Properties(idType)
                .HaveConversion(typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType));
        }

        configurationBuilder.Properties<Money>().HaveConversion<MoneyValueConverter>();
    }
}
