namespace Balboa.SharedKernel;

/// <summary>Non-generic view of an aggregate root, so infrastructure can harvest domain events.</summary>
public interface IAggregateRoot
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    public void ClearDomainEvents();
}
