using System.Reflection;
using Balboa.BuildingBlocks.Cqrs;
using Balboa.BuildingBlocks.Messaging;
using Balboa.BuildingBlocks.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Balboa.BuildingBlocks.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers shared infrastructure once: event bus, domain event dispatcher, clock, startup outbox sweep.</summary>
    public static IServiceCollection AddBuildingBlocks(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IEventBus, InProcessEventBus>();
        services.TryAddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddHostedService<OutboxSweepHostedService>();
        return services;
    }

    /// <summary>
    /// Registers, by convention, every command/query/domain-event/integration-event handler and validator found in
    /// <paramref name="assembly"/> (internal types included) as scoped services.
    /// </summary>
    public static IServiceCollection AddHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(
                classes => classes.AssignableToAny(
                    typeof(ICommandHandler<>),
                    typeof(ICommandHandler<,>),
                    typeof(IQueryHandler<,>),
                    typeof(IDomainEventHandler<>),
                    typeof(IIntegrationEventHandler<>),
                    typeof(IValidator<>)),
                publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        return services;
    }

    /// <summary>
    /// Wraps every registered handler with validation (inner) then logging (outer). Call EXACTLY ONCE, after all
    /// modules have registered their handlers; calling earlier misses handlers, calling twice double-decorates.
    /// </summary>
    public static IServiceCollection AddHandlerDecorators(this IServiceCollection services)
    {
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationCommandHandlerDecorator<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationCommandHandlerDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(ValidationQueryHandlerDecorator<,>));

        services.TryDecorate(typeof(ICommandHandler<>), typeof(LoggingCommandHandlerDecorator<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(LoggingCommandHandlerDecorator<,>));
        services.TryDecorate(typeof(IQueryHandler<,>), typeof(LoggingQueryHandlerDecorator<,>));
        return services;
    }
}
