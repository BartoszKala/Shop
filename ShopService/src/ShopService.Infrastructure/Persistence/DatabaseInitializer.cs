using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ShopService.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Stosuje migracje – razem z nimi trafiają dane przykładowe (kategorie i produkty z <see cref="SeedData"/>).</summary>
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.MigrateAsync(ct);
    }
}
