using Balboa.BuildingBlocks.Cqrs;
using Balboa.BuildingBlocks.Messaging;
using Balboa.BuildingBlocks.Outbox;
using Balboa.Modules.Sample.Application;
using Balboa.Modules.Sample.Contracts;
using Balboa.Modules.Sample.Domain;
using Balboa.Modules.Sample.Infrastructure.Persistence;
using Balboa.SharedKernel;
using Balboa.TestKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;

namespace Balboa.Modules.Sample.IntegrationTests;

/// <summary>Collects delivered integration events, standing in for a subscriber in another module.</summary>
internal sealed class RecordingSubscriber : IIntegrationEventHandler<WidgetPublishedIntegrationEvent>
{
    public List<WidgetPublishedIntegrationEvent> Received { get; } = [];

    public Task HandleAsync(WidgetPublishedIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
    {
        Received.Add(integrationEvent);
        return Task.CompletedTask;
    }
}

[Trait("Category", "Integration")]
public class SchemaTests(PostgresFixture postgres) : SampleModuleTestBase(postgres)
{
    [Fact]
    public async Task Migrate_EmptyDatabase_CreatesTablesAndHistoryOnlyInSampleSchema()
    {
        await using var connection = new NpgsqlConnection(Postgres.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "select table_schema || '.' || table_name from information_schema.tables " +
            "where table_schema in ('sample','public') and table_type = 'BASE TABLE'",
            connection);
        var tables = new List<string>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        tables.ShouldBe(
            [
                "sample.__EFMigrationsHistory",
                "sample.inbox_messages",
                "sample.outbox_messages",
                "sample.widgets",
            ],
            ignoreOrder: true);
    }
}

[Trait("Category", "Integration")]
public class WidgetPersistenceTests(PostgresFixture postgres) : SampleModuleTestBase(postgres)
{
    [Spec("AC-0001-01")]
    [Fact]
    public async Task CreateWidget_ValidCommand_PersistsRowReadableThroughQueries()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ICommandHandler<CreateWidgetCommand, Guid> create = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>();

        Result<Guid> created = await create.HandleAsync(
            new CreateWidgetCommand("int-001", "Integration Gizmo"), TestContext.Current.CancellationToken);

        created.IsSuccess.ShouldBeTrue();
        await using AsyncServiceScope readScope = Services.CreateAsyncScope();
        ISampleQueries queries = readScope.ServiceProvider.GetRequiredService<ISampleQueries>();
        Result<WidgetDto> loaded = await queries.GetWidgetAsync(created.Value, TestContext.Current.CancellationToken);
        loaded.Value.ShouldBe(new WidgetDto(created.Value, "INT-001", "Integration Gizmo", false));
    }

    [Spec("AC-0001-02")]
    [Fact]
    public async Task CreateWidget_InvalidSku_ReturnsValidationErrorAndWritesNothing()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        ICommandHandler<CreateWidgetCommand, Guid> create = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>();

        Result<Guid> result = await create.HandleAsync(
            new CreateWidgetCommand("no", "Gizmo"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(WidgetErrors.InvalidSku);
        (await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Widgets.CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Spec("AC-0001-03")]
    [Fact]
    public async Task CreateWidget_DuplicateSku_ReturnsConflict()
    {
        await using AsyncServiceScope first = Services.CreateAsyncScope();
        await first.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>()
            .HandleAsync(new CreateWidgetCommand("dup-001", "One"), TestContext.Current.CancellationToken);

        await using AsyncServiceScope second = Services.CreateAsyncScope();
        Result<Guid> result = await second.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>()
            .HandleAsync(new CreateWidgetCommand("DUP-001", "Two"), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(WidgetErrors.DuplicateSku);
    }

    [Spec("AC-0001-03")]
    [Fact]
    public async Task SaveChanges_DuplicateSkuBypassingTheHandler_IsRejectedByUniqueIndex()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        SampleDbContext db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        db.Widgets.Add(WidgetCatalogItem.Create("IDX-001", "One").Value);
        db.Widgets.Add(WidgetCatalogItem.Create("IDX-001", "Two").Value);

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Spec("AC-0001-06")]
    [Fact]
    public async Task ListWidgets_TwoPersisted_ReturnsThemOrderedBySku()
    {
        foreach (string sku in new[] { "ZZZ-1", "AAA-1" })
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>()
                .HandleAsync(new CreateWidgetCommand(sku, "Widget " + sku), TestContext.Current.CancellationToken);
        }

        await using AsyncServiceScope readScope = Services.CreateAsyncScope();
        Result<IReadOnlyList<WidgetDto>> list = await readScope.ServiceProvider.GetRequiredService<ISampleQueries>()
            .ListWidgetsAsync(TestContext.Current.CancellationToken);

        list.Value.Select(w => w.Sku).ShouldBe(["AAA-1", "ZZZ-1"]);
    }
}

[Trait("Category", "Integration")]
public class OutboxTests(PostgresFixture postgres) : SampleModuleTestBase(postgres)
{
    private readonly RecordingSubscriber _subscriber = new();

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IIntegrationEventHandler<WidgetPublishedIntegrationEvent>>(_subscriber);

    [Spec("AC-0001-05")]
    [Fact]
    public async Task PublishWidget_Committed_DeliversIntegrationEventOnceAndMarksOutboxProcessed()
    {
        Guid id = await CreateWidgetAsync("OUT-001");

        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        Result published = await scope.ServiceProvider.GetRequiredService<ICommandHandler<PublishWidgetCommand>>()
            .HandleAsync(new PublishWidgetCommand(id), TestContext.Current.CancellationToken);

        published.IsSuccess.ShouldBeTrue();
        WidgetPublishedIntegrationEvent received = _subscriber.Received.ShouldHaveSingleItem();
        received.WidgetId.ShouldBe(id);
        received.Sku.ShouldBe("OUT-001");

        await using AsyncServiceScope verify = Services.CreateAsyncScope();
        OutboxMessage row = await verify.ServiceProvider.GetRequiredService<SampleDbContext>()
            .Set<OutboxMessage>().SingleAsync(TestContext.Current.CancellationToken);
        row.ProcessedOnUtc.ShouldNotBeNull();
        row.Id.ShouldBe(received.EventId);
    }

    [Spec("AC-0001-05")]
    [Fact]
    public async Task PublishWidget_AlreadyPublished_WritesNoSecondOutboxRow()
    {
        Guid id = await CreateWidgetAsync("OUT-002");
        for (int i = 0; i < 2; i++)
        {
            await using AsyncServiceScope scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ICommandHandler<PublishWidgetCommand>>()
                .HandleAsync(new PublishWidgetCommand(id), TestContext.Current.CancellationToken);
        }

        await using AsyncServiceScope verify = Services.CreateAsyncScope();
        (await verify.ServiceProvider.GetRequiredService<SampleDbContext>().Set<OutboxMessage>()
            .CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    private async Task<Guid> CreateWidgetAsync(string sku)
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        Result<Guid> created = await scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>()
            .HandleAsync(new CreateWidgetCommand(sku, "Widget"), TestContext.Current.CancellationToken);
        return created.Value;
    }
}

[Trait("Category", "Integration")]
public class OutboxRecoveryTests(PostgresFixture postgres) : SampleModuleTestBase(postgres)
{
    private readonly IEventBus _bus = Substitute.For<IEventBus>();

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IEventBus>();
        services.AddSingleton(_bus);
    }

    [Spec("AC-0001-05")]
    [Fact]
    public async Task ProcessPending_DeliveryFailedAtCommit_KeepsRowPendingAndSweepDeliversIt()
    {
        _bus.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("subscriber down")));

        Guid id;
        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateWidgetCommand, Guid>>()
                .HandleAsync(new CreateWidgetCommand("REC-001", "Widget"), TestContext.Current.CancellationToken)).Value;
        }

        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            Result published = await scope.ServiceProvider.GetRequiredService<ICommandHandler<PublishWidgetCommand>>()
                .HandleAsync(new PublishWidgetCommand(id), TestContext.Current.CancellationToken);
            published.IsSuccess.ShouldBeTrue("a delivery failure must not fail a committed command");
        }

        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            OutboxMessage pending = await scope.ServiceProvider.GetRequiredService<SampleDbContext>()
                .Set<OutboxMessage>().SingleAsync(TestContext.Current.CancellationToken);
            pending.ProcessedOnUtc.ShouldBeNull();
            pending.Attempts.ShouldBe(1);
            pending.LastError.ShouldBe("subscriber down");
        }

        _bus.ClearReceivedCalls();
        _bus.PublishAsync(Arg.Any<IIntegrationEvent>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await using (AsyncServiceScope sweep = Services.CreateAsyncScope())
        {
            int delivered = await sweep.ServiceProvider.GetRequiredService<IOutboxProcessor>()
                .ProcessPendingAsync(TestContext.Current.CancellationToken);
            delivered.ShouldBe(1);
        }

        await _bus.Received(1).PublishAsync(
            Arg.Is<IIntegrationEvent>(e => ((WidgetPublishedIntegrationEvent)e).WidgetId == id),
            Arg.Any<CancellationToken>());
    }
}

[Trait("Category", "Integration")]
public class InboxTests(PostgresFixture postgres) : SampleModuleTestBase(postgres)
{
    [Fact]
    public async Task TryRegister_SameEventAndConsumerTwice_ReturnsTrueThenFalse()
    {
        Guid eventId = TestData.NewGuid();

        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            SampleDbContext db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            var inbox = new EfInbox<SampleDbContext>(db, TimeProvider.System);
            (await inbox.TryRegisterAsync(eventId, "billing", TestContext.Current.CancellationToken)).ShouldBeTrue();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (AsyncServiceScope scope = Services.CreateAsyncScope())
        {
            var inbox = new EfInbox<SampleDbContext>(scope.ServiceProvider.GetRequiredService<SampleDbContext>(), TimeProvider.System);
            (await inbox.TryRegisterAsync(eventId, "billing", TestContext.Current.CancellationToken)).ShouldBeFalse();
            (await inbox.TryRegisterAsync(eventId, "shipping", TestContext.Current.CancellationToken)).ShouldBeTrue();
        }
    }
}
