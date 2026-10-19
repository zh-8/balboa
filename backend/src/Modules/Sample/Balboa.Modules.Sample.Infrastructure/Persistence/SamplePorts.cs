using Balboa.BuildingBlocks.Cqrs;
using Balboa.BuildingBlocks.Outbox;
using Balboa.Modules.Sample.Application;
using Balboa.Modules.Sample.Domain;
using Balboa.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balboa.Modules.Sample.Infrastructure.Persistence;

internal sealed class WidgetCatalogRepository(SampleDbContext db) : IWidgetCatalogRepository
{
    public Task<WidgetCatalogItem?> GetAsync(WidgetId id, CancellationToken cancellationToken = default) =>
        db.Widgets.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken = default) =>
        db.Widgets.AnyAsync(w => w.Sku == sku, cancellationToken);

    public async Task<IReadOnlyList<WidgetCatalogItem>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.Widgets.AsNoTracking().OrderBy(w => w.Sku).ToListAsync(cancellationToken);

    public void Add(WidgetCatalogItem item) => db.Widgets.Add(item);
}

internal sealed class SampleOutbox(SampleDbContext db) : EfOutbox<SampleDbContext>(db), ISampleOutbox;

internal static partial class UnitOfWorkLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Post-commit outbox delivery failed; messages stay pending")]
    public static partial void DeliveryFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Commit protocol: dispatch domain events (handlers may enqueue outbox rows) -> one SaveChanges (aggregate
/// and outbox rows commit atomically) -> deliver pending outbox messages in-process.
/// </summary>
internal sealed class SampleUnitOfWork(
    SampleDbContext db,
    IDomainEventDispatcher dispatcher,
    OutboxProcessor<SampleDbContext> outboxProcessor,
    ILogger<SampleUnitOfWork> logger) : ISampleUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        List<IAggregateRoot> aggregates = [.. db.ChangeTracker.Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)];
        List<IDomainEvent> events = [.. aggregates.SelectMany(a => a.DomainEvents)];
        aggregates.ForEach(a => a.ClearDomainEvents());

        await dispatcher.DispatchAsync(events, cancellationToken);
        int written = await db.SaveChangesAsync(cancellationToken);

        try
        {
            await outboxProcessor.ProcessPendingAsync(cancellationToken);
        }
#pragma warning disable CA1031 // The change is committed; a delivery problem must not fail the command. Sweep retries.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            UnitOfWorkLog.DeliveryFailed(logger, ex);
        }

        return written;
    }
}
