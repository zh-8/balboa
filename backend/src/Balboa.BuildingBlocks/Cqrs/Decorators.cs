using System.Diagnostics;
using Balboa.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Balboa.BuildingBlocks.Cqrs;

internal static partial class HandlerLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Handling {RequestName}")]
    public static partial void Handling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs} ms")]
    public static partial void Handled(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "{RequestName} failed in {ElapsedMs} ms with {ErrorCode}: {ErrorDescription}")]
    public static partial void Failed(
        ILogger logger, string requestName, long elapsedMs, string errorCode, string errorDescription);

    public static void Completed(ILogger logger, string requestName, long startedAt, Result result)
    {
        long elapsed = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        if (result.IsSuccess)
        {
            Handled(logger, requestName, elapsed);
        }
        else
        {
            Failed(logger, requestName, elapsed, result.Error.Code, result.Error.Description);
        }
    }
}

internal static class Validation
{
    public static Result Run<T>(IEnumerable<IValidator<T>> validators, T request)
    {
        foreach (IValidator<T> validator in validators)
        {
            Result result = validator.Validate(request);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result.Success();
    }
}

// ---- Logging decorators (outermost) ----

internal sealed class LoggingCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    ILogger<ICommandHandler<TCommand>> logger) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        HandlerLog.Handling(logger, typeof(TCommand).Name);
        long startedAt = Stopwatch.GetTimestamp();
        Result result = await inner.HandleAsync(command, cancellationToken);
        HandlerLog.Completed(logger, typeof(TCommand).Name, startedAt, result);
        return result;
    }
}

internal sealed class LoggingCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    ILogger<ICommandHandler<TCommand, TResult>> logger) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        HandlerLog.Handling(logger, typeof(TCommand).Name);
        long startedAt = Stopwatch.GetTimestamp();
        Result<TResult> result = await inner.HandleAsync(command, cancellationToken);
        HandlerLog.Completed(logger, typeof(TCommand).Name, startedAt, result);
        return result;
    }
}

internal sealed class LoggingQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    ILogger<IQueryHandler<TQuery, TResult>> logger) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        HandlerLog.Handling(logger, typeof(TQuery).Name);
        long startedAt = Stopwatch.GetTimestamp();
        Result<TResult> result = await inner.HandleAsync(query, cancellationToken);
        HandlerLog.Completed(logger, typeof(TQuery).Name, startedAt, result);
        return result;
    }
}

// ---- Validation decorators (inside logging) ----

internal sealed class ValidationCommandHandlerDecorator<TCommand>(
    ICommandHandler<TCommand> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Result validation = Validation.Run(validators, command);
        return validation.IsFailure ? Task.FromResult(validation) : inner.HandleAsync(command, cancellationToken);
    }
}

internal sealed class ValidationCommandHandlerDecorator<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default)
    {
        Result validation = Validation.Run(validators, command);
        return validation.IsFailure
            ? Task.FromResult(Result.Failure<TResult>(validation.Error))
            : inner.HandleAsync(command, cancellationToken);
    }
}

internal sealed class ValidationQueryHandlerDecorator<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken = default)
    {
        Result validation = Validation.Run(validators, query);
        return validation.IsFailure
            ? Task.FromResult(Result.Failure<TResult>(validation.Error))
            : inner.HandleAsync(query, cancellationToken);
    }
}
