using Balboa.BuildingBlocks.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Balboa.Host.ApiTests;

/// <summary>Boots the real Host in-process, pointed at the Testcontainers database.</summary>
public sealed class BalboaApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:balboa", connectionString);
        builder.UseSetting("FeatureManagement:ExampleFlag", "true");
    }

    /// <summary>Applies every module's real migrations, exactly like `Balboa.Host migrate`.</summary>
    public async Task MigrateAsync()
    {
        await using AsyncServiceScope scope = Services.CreateAsyncScope();
        foreach (IModuleMigrator migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
        {
            await migrator.MigrateAsync();
        }
    }
}
