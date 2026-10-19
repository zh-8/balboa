using Balboa.BuildingBlocks.Cqrs;
using Balboa.BuildingBlocks.Messaging;
using Balboa.Modules.Sample.Domain;

namespace Balboa.Modules.Sample.Application;

/// <summary>Persistence port for the aggregate. Implemented by Infrastructure.</summary>
internal interface IWidgetCatalogRepository
{
    public Task<WidgetCatalogItem?> GetAsync(WidgetId id, CancellationToken cancellationToken = default);

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<WidgetCatalogItem>> ListAsync(CancellationToken cancellationToken = default);

    public void Add(WidgetCatalogItem item);
}

/// <summary>This module's unit of work (distinct from every other module's).</summary>
internal interface ISampleUnitOfWork : IUnitOfWork;

/// <summary>This module's outbox (distinct from every other module's).</summary>
internal interface ISampleOutbox : IOutbox;
