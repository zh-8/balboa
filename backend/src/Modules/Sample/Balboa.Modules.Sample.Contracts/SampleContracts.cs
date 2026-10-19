using Balboa.BuildingBlocks.Messaging;
using Balboa.SharedKernel;

namespace Balboa.Modules.Sample.Contracts;

/// <summary>Read model of a widget catalog item.</summary>
public sealed record WidgetDto(Guid Id, string Sku, string Name, bool IsPublished);

public sealed record CreateWidgetRequest(string Sku, string Name);

/// <summary>Published when a widget becomes visible in the catalog. Delivered via the transactional outbox.</summary>
public sealed record WidgetPublishedIntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredOn,
    Guid WidgetId,
    string Sku) : IIntegrationEvent;

/// <summary>What other modules may ask the Sample module. They must never reach into its internals.</summary>
public interface ISampleQueries
{
    public Task<Result<WidgetDto>> GetWidgetAsync(Guid id, CancellationToken cancellationToken = default);

    public Task<Result<IReadOnlyList<WidgetDto>>> ListWidgetsAsync(CancellationToken cancellationToken = default);
}
