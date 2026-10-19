using Balboa.BuildingBlocks.Cqrs;
using Balboa.Modules.Sample.Application;
using Balboa.Modules.Sample.Contracts;
using Balboa.Modules.Sample.Domain;
using Balboa.SharedKernel;

namespace Balboa.Modules.Sample.UnitTests;

/// <summary>In-memory stand-ins for the ports; hand-written because the ports are internal.</summary>
internal sealed class FakeWidgetRepository : IWidgetCatalogRepository
{
    public List<WidgetCatalogItem> Items { get; } = [];

    public Task<WidgetCatalogItem?> GetAsync(WidgetId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public Task<bool> SkuExistsAsync(Sku sku, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(i => i.Sku == sku));

    public Task<IReadOnlyList<WidgetCatalogItem>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<WidgetCatalogItem>>([.. Items]);

    public void Add(WidgetCatalogItem item) => Items.Add(item);
}

internal sealed class FakeUnitOfWork : ISampleUnitOfWork
{
    public int Commits { get; private set; }

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        Commits++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeOutbox : ISampleOutbox
{
    public List<Balboa.BuildingBlocks.Messaging.IIntegrationEvent> Enqueued { get; } = [];

    public void Enqueue(Balboa.BuildingBlocks.Messaging.IIntegrationEvent integrationEvent) => Enqueued.Add(integrationEvent);
}

public class CreateWidgetHandlerTests
{
    private readonly FakeWidgetRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Spec("AC-0001-01")]
    [Fact]
    public async Task HandleAsync_ValidCommand_AddsWidgetAndCommits()
    {
        var handler = new CreateWidgetHandler(_repository, _unitOfWork);

        Result<Guid> result = await handler.HandleAsync(new CreateWidgetCommand("wid-001", "Gizmo"));

        result.IsSuccess.ShouldBeTrue();
        _repository.Items.ShouldHaveSingleItem().Id.Value.ShouldBe(result.Value);
        _unitOfWork.Commits.ShouldBe(1);
    }

    [Spec("AC-0001-02")]
    [Fact]
    public async Task HandleAsync_InvalidSku_ReturnsValidationErrorAndDoesNotCommit()
    {
        var handler = new CreateWidgetHandler(_repository, _unitOfWork);

        Result<Guid> result = await handler.HandleAsync(new CreateWidgetCommand("x", "Gizmo"));

        result.Error.ShouldBe(WidgetErrors.InvalidSku);
        _repository.Items.ShouldBeEmpty();
        _unitOfWork.Commits.ShouldBe(0);
    }

    [Spec("AC-0001-03")]
    [Fact]
    public async Task HandleAsync_DuplicateSku_ReturnsConflictAndDoesNotCommit()
    {
        var handler = new CreateWidgetHandler(_repository, _unitOfWork);
        await handler.HandleAsync(new CreateWidgetCommand("wid-001", "Gizmo"));

        Result<Guid> second = await handler.HandleAsync(new CreateWidgetCommand("WID-001", "Other"));

        second.Error.ShouldBe(WidgetErrors.DuplicateSku);
        _repository.Items.Count.ShouldBe(1);
        _unitOfWork.Commits.ShouldBe(1);
    }
}

public class PublishWidgetHandlerTests
{
    private readonly FakeWidgetRepository _repository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    [Spec("AC-0001-04")]
    [Fact]
    public async Task HandleAsync_ExistingWidget_PublishesAndCommits()
    {
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;
        _repository.Add(item);
        var handler = new PublishWidgetHandler(_repository, _unitOfWork);

        Result result = await handler.HandleAsync(new PublishWidgetCommand(item.Id.Value));

        result.IsSuccess.ShouldBeTrue();
        item.IsPublished.ShouldBeTrue();
        _unitOfWork.Commits.ShouldBe(1);
    }

    [Spec("AC-0001-04")]
    [Fact]
    public async Task HandleAsync_AlreadyPublished_ReturnsConflictAndDoesNotCommit()
    {
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;
        item.Publish();
        _repository.Add(item);
        var handler = new PublishWidgetHandler(_repository, _unitOfWork);

        Result result = await handler.HandleAsync(new PublishWidgetCommand(item.Id.Value));

        result.Error.ShouldBe(WidgetErrors.AlreadyPublished);
        _unitOfWork.Commits.ShouldBe(0);
    }

    [Spec("AC-0001-04")]
    [Fact]
    public async Task HandleAsync_UnknownWidget_ReturnsNotFound()
    {
        var handler = new PublishWidgetHandler(_repository, _unitOfWork);

        Result result = await handler.HandleAsync(new PublishWidgetCommand(Guid.NewGuid()));

        result.Error.ShouldBe(WidgetErrors.NotFound);
    }

    [Spec("AC-0001-04")]
    [Fact]
    public void Validate_EmptyId_ReturnsValidationError()
    {
        Result result = new PublishWidgetCommandValidator().Validate(new PublishWidgetCommand(Guid.Empty));

        result.Error.Type.ShouldBe(ErrorType.Validation);
        new PublishWidgetCommandValidator().Validate(new PublishWidgetCommand(Guid.NewGuid())).IsSuccess.ShouldBeTrue();
    }
}

public class WidgetPublishedDomainEventHandlerTests
{
    [Spec("AC-0001-05")]
    [Fact]
    public async Task HandleAsync_WidgetPublished_EnqueuesMatchingIntegrationEvent()
    {
        var outbox = new FakeOutbox();
        var handler = new WidgetPublishedDomainEventHandler(outbox);
        var domainEvent = new WidgetPublished(WidgetId.New(), "WID-001");

        await handler.HandleAsync(domainEvent);

        WidgetPublishedIntegrationEvent published =
            outbox.Enqueued.ShouldHaveSingleItem().ShouldBeOfType<WidgetPublishedIntegrationEvent>();
        published.WidgetId.ShouldBe(domainEvent.WidgetId.Value);
        published.Sku.ShouldBe("WID-001");
        published.EventId.ShouldBe(domainEvent.EventId);
    }
}

public class WidgetQueryTests
{
    [Spec("AC-0001-06")]
    [Fact]
    public async Task GetWidget_Existing_ReturnsDto()
    {
        var repository = new FakeWidgetRepository();
        WidgetCatalogItem item = WidgetCatalogItem.Create("WID-001", "Gizmo").Value;
        repository.Add(item);

        Result<WidgetDto> result = await new GetWidgetHandler(repository).HandleAsync(new GetWidgetQuery(item.Id.Value));

        result.Value.ShouldBe(new WidgetDto(item.Id.Value, "WID-001", "Gizmo", false));
    }

    [Spec("AC-0001-06")]
    [Fact]
    public async Task GetWidget_Unknown_ReturnsNotFound()
    {
        Result<WidgetDto> result =
            await new GetWidgetHandler(new FakeWidgetRepository()).HandleAsync(new GetWidgetQuery(Guid.NewGuid()));

        result.Error.ShouldBe(WidgetErrors.NotFound);
    }

    [Spec("AC-0001-06")]
    [Fact]
    public async Task ListWidgets_TwoWidgets_ReturnsBoth()
    {
        var repository = new FakeWidgetRepository();
        repository.Add(WidgetCatalogItem.Create("AAA-1", "A").Value);
        repository.Add(WidgetCatalogItem.Create("BBB-2", "B").Value);
        var queries = new SampleQueries(
            new GetWidgetHandler(repository), new ListWidgetsHandler(repository));

        Result<IReadOnlyList<WidgetDto>> result = await queries.ListWidgetsAsync();

        result.Value.Select(w => w.Sku).ShouldBe(["AAA-1", "BBB-2"]);
    }
}
