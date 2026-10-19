using System.Collections.Concurrent;
using System.Reflection;
using Balboa.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Balboa.BuildingBlocks.Cqrs;

public interface IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}

/// <summary>Resolves <see cref="IDomainEventHandler{TDomainEvent}"/> implementations from DI and invokes them in order.</summary>
internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Method)> _cache = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (IDomainEvent domainEvent in domainEvents)
        {
            (Type handlerType, MethodInfo method) = _cache.GetOrAdd(domainEvent.GetType(), static eventType =>
            {
                Type type = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
                return (type, type.GetMethod(nameof(IDomainEventHandler<>.HandleAsync))!);
            });

            foreach (object? handler in services.GetServices(handlerType))
            {
                await (Task)method.Invoke(handler, [domainEvent, cancellationToken])!;
            }
        }
    }
}
