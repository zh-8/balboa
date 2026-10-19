using Balboa.BuildingBlocks.Cqrs;
using Balboa.Modules.Sample.Contracts;
using Balboa.Modules.Sample.Domain;
using Balboa.SharedKernel;

namespace Balboa.Modules.Sample.Application;

// ---- commands and queries ----

internal sealed record CreateWidgetCommand(string? Sku, string? Name) : ICommand<Guid>;

internal sealed record PublishWidgetCommand(Guid WidgetId) : ICommand;

internal sealed record GetWidgetQuery(Guid WidgetId) : IQuery<WidgetDto>;

internal sealed record ListWidgetsQuery : IQuery<IReadOnlyList<WidgetDto>>;

// ---- validation (request shape only; business rules live in the domain) ----

internal sealed class PublishWidgetCommandValidator : IValidator<PublishWidgetCommand>
{
    public Result Validate(PublishWidgetCommand request) =>
        request.WidgetId == Guid.Empty
            ? Result.Failure(Error.Validation("Widget.IdRequired", "A widget id is required."))
            : Result.Success();
}

// ---- handlers ----

internal sealed class CreateWidgetHandler(IWidgetCatalogRepository widgets, ISampleUnitOfWork unitOfWork)
    : ICommandHandler<CreateWidgetCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(CreateWidgetCommand command, CancellationToken cancellationToken = default)
    {
        Result<WidgetCatalogItem> created = WidgetCatalogItem.Create(command.Sku, command.Name);
        if (created.IsFailure)
        {
            return Result.Failure<Guid>(created.Error);
        }

        WidgetCatalogItem item = created.Value;
        if (await widgets.SkuExistsAsync(item.Sku, cancellationToken))
        {
            return Result.Failure<Guid>(WidgetErrors.DuplicateSku);
        }

        widgets.Add(item);
        await unitOfWork.CommitAsync(cancellationToken);
        return item.Id.Value;
    }
}

internal sealed class PublishWidgetHandler(IWidgetCatalogRepository widgets, ISampleUnitOfWork unitOfWork)
    : ICommandHandler<PublishWidgetCommand>
{
    public async Task<Result> HandleAsync(PublishWidgetCommand command, CancellationToken cancellationToken = default)
    {
        WidgetCatalogItem? item = await widgets.GetAsync(WidgetId.From(command.WidgetId), cancellationToken);
        if (item is null)
        {
            return Result.Failure(WidgetErrors.NotFound);
        }

        Result published = item.Publish();
        if (published.IsFailure)
        {
            return published;
        }

        await unitOfWork.CommitAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class GetWidgetHandler(IWidgetCatalogRepository widgets) : IQueryHandler<GetWidgetQuery, WidgetDto>
{
    public async Task<Result<WidgetDto>> HandleAsync(GetWidgetQuery query, CancellationToken cancellationToken = default)
    {
        WidgetCatalogItem? item = await widgets.GetAsync(WidgetId.From(query.WidgetId), cancellationToken);
        return item is null ? Result.Failure<WidgetDto>(WidgetErrors.NotFound) : item.ToDto();
    }
}

internal sealed class ListWidgetsHandler(IWidgetCatalogRepository widgets)
    : IQueryHandler<ListWidgetsQuery, IReadOnlyList<WidgetDto>>
{
    public async Task<Result<IReadOnlyList<WidgetDto>>> HandleAsync(
        ListWidgetsQuery query, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WidgetCatalogItem> items = await widgets.ListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<WidgetDto>>([.. items.Select(i => i.ToDto())]);
    }
}

internal static class WidgetMappings
{
    public static WidgetDto ToDto(this WidgetCatalogItem item) =>
        new(item.Id.Value, item.Sku.Value, item.Name, item.IsPublished);
}

// ---- domain event -> integration event (written to the outbox in the same transaction) ----

internal sealed class WidgetPublishedDomainEventHandler(ISampleOutbox outbox) : IDomainEventHandler<WidgetPublished>
{
    public Task HandleAsync(WidgetPublished domainEvent, CancellationToken cancellationToken = default)
    {
        outbox.Enqueue(new WidgetPublishedIntegrationEvent(
            domainEvent.EventId, domainEvent.OccurredOn, domainEvent.WidgetId.Value, domainEvent.Sku));
        return Task.CompletedTask;
    }
}

// ---- published query surface: other modules go through this, never through the handlers ----

internal sealed class SampleQueries(
    IQueryHandler<GetWidgetQuery, WidgetDto> getWidget,
    IQueryHandler<ListWidgetsQuery, IReadOnlyList<WidgetDto>> listWidgets) : ISampleQueries
{
    public Task<Result<WidgetDto>> GetWidgetAsync(Guid id, CancellationToken cancellationToken = default) =>
        getWidget.HandleAsync(new GetWidgetQuery(id), cancellationToken);

    public Task<Result<IReadOnlyList<WidgetDto>>> ListWidgetsAsync(CancellationToken cancellationToken = default) =>
        listWidgets.HandleAsync(new ListWidgetsQuery(), cancellationToken);
}
