using System.Text.Json;
using Balboa.BuildingBlocks.Messaging;

namespace Balboa.BuildingBlocks.Outbox;

/// <summary>Integration event persisted in the same transaction as the aggregate change that caused it.</summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Assembly-qualified CLR type of the event, used to deserialize it on delivery.</summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public DateTimeOffset? ProcessedOnUtc { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage From(IIntegrationEvent integrationEvent)
    {
        Type type = integrationEvent.GetType();
        return new OutboxMessage
        {
            Id = integrationEvent.EventId,
            Type = type.AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(integrationEvent, type),
            OccurredOnUtc = integrationEvent.OccurredOn,
        };
    }

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedOnUtc = now;
        LastError = null;
        Attempts++;
    }

    public void MarkFailed(string error)
    {
        LastError = error.Length > 2000 ? error[..2000] : error;
        Attempts++;
    }
}

/// <summary>Records that a subscriber has already handled an event, so redelivery is harmless.</summary>
public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public Guid EventId { get; private set; }

    public string Consumer { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedOnUtc { get; private set; }

    public static InboxMessage Create(Guid eventId, string consumer, DateTimeOffset now) =>
        new() { EventId = eventId, Consumer = consumer, ReceivedOnUtc = now };
}
