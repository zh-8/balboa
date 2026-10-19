using System.Text.Json;
using Balboa.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Balboa.BuildingBlocks.Outbox;

public class EfOutbox<TDbContext>(TDbContext db) : IOutbox
    where TDbContext : DbContext
{
    public void Enqueue(IIntegrationEvent integrationEvent) => db.Set<OutboxMessage>().Add(OutboxMessage.From(integrationEvent));
}

public sealed class EfInbox<TDbContext>(TDbContext db, TimeProvider clock) : IInbox
    where TDbContext : DbContext
{
    public async Task<bool> TryRegisterAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default)
    {
        bool seen = await db.Set<InboxMessage>().AnyAsync(m => m.EventId == eventId && m.Consumer == consumer, cancellationToken);
        if (seen)
        {
            return false;
        }

        db.Set<InboxMessage>().Add(InboxMessage.Create(eventId, consumer, clock.GetUtcNow()));
        return true;
    }
}

internal static partial class OutboxLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox message {MessageId} ({MessageType}) failed to deliver")]
    public static partial void DeliveryFailed(ILogger logger, Exception exception, Guid messageId, string messageType);
}

/// <summary>
/// Delivers undelivered outbox rows via <see cref="IEventBus"/> and marks them processed. At-least-once:
/// a failure leaves the row pending for the next sweep, so subscribers must be idempotent.
/// </summary>
public sealed class OutboxProcessor<TDbContext>(
    TDbContext db,
    IEventBus eventBus,
    TimeProvider clock,
    ILogger<OutboxProcessor<TDbContext>> logger) : IOutboxProcessor
    where TDbContext : DbContext
{
    private const int _batchSize = 100;

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        List<OutboxMessage> pending = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        int delivered = 0;
        foreach (OutboxMessage message in pending)
        {
            try
            {
                Type type = Type.GetType(message.Type, throwOnError: true)!;
                if (!typeof(IIntegrationEvent).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException($"{message.Type} is not an integration event.");
                }

                var integrationEvent = (IIntegrationEvent)JsonSerializer.Deserialize(message.Payload, type)!;
                await eventBus.PublishAsync(integrationEvent, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow());
                delivered++;
            }
#pragma warning disable CA1031 // A bad subscriber must not poison the batch; the row stays pending and is retried.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                OutboxLog.DeliveryFailed(logger, ex, message.Id, message.Type);
                message.MarkFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return delivered;
    }
}
