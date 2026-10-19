namespace Balboa.SharedKernel;

/// <summary>Something that happened inside a module's domain. Never leaves the module.</summary>
public interface IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredOn { get; }
}

/// <summary>Convenience base record that stamps an id and a UTC timestamp.</summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTimeOffset OccurredOn { get; init; } = DateTimeOffset.UtcNow;
}
