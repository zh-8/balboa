using Balboa.TestKit;
using Xunit;

namespace Balboa.Host.ApiTests;

public abstract class ApiTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected BalboaApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        Factory = new BalboaApiFactory(postgres.ConnectionString);
        await Factory.MigrateAsync();
        await postgres.ResetAsync("sample");
        Client = Factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
