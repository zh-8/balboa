using System.Net;
using System.Net.Http.Json;
using Balboa.TestKit;

namespace Balboa.Host.ApiTests;

[Trait("Category", "Integration")]
public class PlatformEndpointTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task HealthLive_ProcessRunning_Returns200Healthy()
    {
        HttpResponseMessage response = await Client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthReady_DatabaseReachableAndMigrated_Returns200Healthy()
    {
        HttpResponseMessage response = await Client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Features_FlagSetInConfiguration_ReturnsItsValue()
    {
        Dictionary<string, bool>? flags = await Client.GetFromJsonAsync<Dictionary<string, bool>>(
            "/api/features", TestContext.Current.CancellationToken);

        flags.ShouldNotBeNull();
        flags["ExampleFlag"].ShouldBeTrue();
    }

    [Fact]
    public async Task OpenApi_NonProductionEnvironment_ServesDocumentListingSampleRoutes()
    {
        string document = await Client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        document.ShouldContain("/api/sample/widgets");
        document.ShouldContain("/api/features");
    }
}

[Trait("Category", "Integration")]
public class ReadinessWhenDatabaseDownTests
{
    [Fact]
    public async Task HealthReady_DatabaseUnreachable_Returns503ButLiveStaysHealthy()
    {
        await using var factory = new BalboaApiFactory("Host=127.0.0.1;Port=1;Database=none;Username=x;Timeout=2");
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        HttpResponseMessage live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        ready.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
