using Balboa.BuildingBlocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Balboa.BuildingBlocks.Outbox;

internal static partial class SweepLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox startup sweep failed; pending messages will be retried on next commit or start")]
    public static partial void Failed(ILogger logger, Exception exception);
}

/// <summary>
/// Drains leftover outbox rows once at startup. Scale-to-zero makes a background poller unreliable, so
/// delivery is driven by commits plus this sweep rather than a timer.
/// </summary>
internal sealed class OutboxSweepHostedService(IServiceScopeFactory scopes, ILogger<OutboxSweepHostedService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            foreach (IOutboxProcessor processor in scope.ServiceProvider.GetServices<IOutboxProcessor>())
            {
                await processor.ProcessPendingAsync(cancellationToken);
            }
        }
#pragma warning disable CA1031 // Startup must never be blocked by a delivery problem (e.g. migrations not yet applied).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            SweepLog.Failed(logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
