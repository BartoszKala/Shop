using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShopService.Application.Abstractions;
using ShopService.Application.Common;
using ShopService.Domain.Common;

namespace ShopService.Infrastructure.Persistence;

internal sealed class UnitOfWork(ShopDbContext db, IServiceProvider services) : IUnitOfWork
{
    private const int MaxDispatchRounds = 10;

    private static readonly MethodInfo DispatchMethod =
        typeof(UnitOfWork).GetMethod(nameof(DispatchAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task<Result> SaveChangesAsync(CancellationToken ct = default)
    {
        // Handlery zmieniają encje i dopisują wiadomości do outboxu w tym samym kontekście – jeden SaveChanges = jedna transakcja
        await DispatchDomainEventsAsync(ct);

        try
        {
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return CommonErrors.ConcurrencyConflict;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return CommonErrors.Duplicate;
        }
    }

    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        // Handler może zmienić inny agregat, który zgłosi kolejne zdarzenia – rozsyłamy do skutku (z bezpiecznikiem)
        for (var round = 0; round < MaxDispatchRounds; round++)
        {
            var aggregates = db.ChangeTracker.Entries<AggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0)
                .ToList();
            if (aggregates.Count == 0)
            {
                return;
            }

            var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();
            aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());

            foreach (var domainEvent in domainEvents)
            {
                await (Task)DispatchMethod.MakeGenericMethod(domainEvent.GetType()).Invoke(this, [domainEvent, ct])!;
            }
        }

        throw new InvalidOperationException($"Domain events still pending after {MaxDispatchRounds} dispatch rounds - check handlers for cycles.");
    }

    private async Task DispatchAsync<TEvent>(TEvent domainEvent, CancellationToken ct) where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
        {
            await handler.HandleAsync(domainEvent, ct);
        }
    }
}
