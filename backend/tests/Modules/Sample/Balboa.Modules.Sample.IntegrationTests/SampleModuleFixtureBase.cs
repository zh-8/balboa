using Balboa.BuildingBlocks.DependencyInjection;
using Balboa.BuildingBlocks.Messaging;
using Balboa.Modules.Sample.Infrastructure;
using Balboa.Modules.Sample.Infrastructure.Persistence;
using Balboa.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Balboa.Modules.Sample.IntegrationTests;

/// <summary>
/// Boots the Sample module exactly as the Host does (AddSampleModule + decorators) against the shared
/// Postgres container, applies the REAL migrations once, and wipes data before every test.
/// </summary>
public abstract class SampleModuleTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly SemaphoreSlim _migrationGate = new(1, 1);
    private static bool _migrated;

    protected PostgresFixture Postgres { get; } = postgres;

    protected ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Services = BuildServices(ConfigureServices);

        await _migrationGate.WaitAsync();
        try
        {
            if (!_migrated)
            {
                await using AsyncServiceScope scope = Services.CreateAsyncScope();
                foreach (IModuleMigrator migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
                {
                    await migrator.MigrateAsync();
                }

                _migrated = true;
            }
        }
        finally
        {
            _migrationGate.Release();
        }

        await Postgres.ResetAsync(SampleDbContext.Schema);
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    private ServiceProvider BuildServices(Action<IServiceCollection> configure)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"ConnectionStrings:{SampleModule.ConnectionStringName}"] = Postgres.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSampleModule(configuration);
        configure(services);
        services.AddHandlerDecorators();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
