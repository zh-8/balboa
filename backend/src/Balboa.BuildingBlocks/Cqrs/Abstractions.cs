using Balboa.SharedKernel;

namespace Balboa.BuildingBlocks.Cqrs;

/// <summary>Marker for a command that changes state and returns no value.</summary>
public interface ICommand;

/// <summary>Marker for a command that changes state and returns a value.</summary>
public interface ICommand<TResult>;

/// <summary>Marker for a read-only query.</summary>
public interface IQuery<TResult>;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    public Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}

/// <summary>Request validation, run by the validation decorator before a handler executes.</summary>
public interface IValidator<in TRequest>
{
    public Result Validate(TRequest request);
}

/// <summary>Reacts to a domain event inside the module that raised it, before the unit of work commits.</summary>
public interface IDomainEventHandler<in TDomainEvent>
    where TDomainEvent : IDomainEvent
{
    public Task HandleAsync(TDomainEvent domainEvent, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    /// <summary>Dispatches domain events, then commits all changes (including outbox rows) atomically.</summary>
    public Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
