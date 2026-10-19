namespace Balboa.BuildingBlocks.Messaging;

/// <summary>A fact one module publishes for others. Lives in the publisher's .Contracts project.</summary>
public interface IIntegrationEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredOn { get; }
}

public interface IEventBus
{
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}

/// <summary>Subscriber to an integration event. Must be idempotent; use <see cref="IInbox"/>.</summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    public Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken = default);
}

/// <summary>Writes an integration event to the module's outbox in the current unit of work.</summary>
public interface IOutbox
{
    public void Enqueue(IIntegrationEvent integrationEvent);
}

/// <summary>Idempotency guard for subscribers; the inbox row commits with the handler's own changes.</summary>
public interface IInbox
{
    /// <returns>True the first time an (event, consumer) pair is seen; false for a duplicate.</returns>
    public Task<bool> TryRegisterAsync(Guid eventId, string consumer, CancellationToken cancellationToken = default);
}

/// <summary>Delivers committed-but-undelivered outbox messages. Called after commit and by the startup sweep.</summary>
public interface IOutboxProcessor
{
    public Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
}

/// <summary>Applies one module's EF migrations. Registered by each module; used by the Host `migrate` command.</summary>
public interface IModuleMigrator
{
    public string ModuleName { get; }

    public Task MigrateAsync(CancellationToken cancellationToken = default);
}
