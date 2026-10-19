using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Balboa.BuildingBlocks.Messaging;

/// <summary>In-process delivery: invokes every registered <see cref="IIntegrationEventHandler{TEvent}"/>.</summary>
internal sealed class InProcessEventBus(IServiceProvider services) : IEventBus
{
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Method)> _cache = new();

    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        (Type handlerType, MethodInfo method) = _cache.GetOrAdd(integrationEvent.GetType(), static eventType =>
        {
            Type type = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            return (type, type.GetMethod(nameof(IIntegrationEventHandler<>.HandleAsync))!);
        });

        foreach (object? handler in services.GetServices(handlerType))
        {
            await (Task)method.Invoke(handler, [integrationEvent, cancellationToken])!;
        }
    }
}
