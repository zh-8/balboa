using Balboa.BuildingBlocks.DependencyInjection;
using Balboa.BuildingBlocks.Messaging;
using Balboa.Modules.Sample.Infrastructure;
using Balboa.ServiceDefaults;
using Microsoft.FeatureManagement;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddFeatureManagement();

// ---- modules: add one line per module here and in the endpoint section below ----
builder.Services.AddSampleModule(builder.Configuration);

// Must come after every module has registered its handlers (see AddHandlerDecorators).
builder.Services.AddHandlerDecorators();

WebApplication app = builder.Build();

// `dotnet run --project backend/src/Balboa.Host -- migrate`: apply every module's migrations, then exit.
if (args.Contains("migrate", StringComparer.OrdinalIgnoreCase))
{
    await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
    ILogger logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrate");
    foreach (IModuleMigrator migrator in scope.ServiceProvider.GetServices<IModuleMigrator>())
    {
        MigrateLog.Migrating(logger, migrator.ModuleName);
        await migrator.MigrateAsync();
    }

    MigrateLog.Done(logger);
    return 0;
}

if (!app.Environment.IsProduction())
{
    app.MapOpenApi();
}

app.MapDefaultEndpoints();

app.MapGet("/api/features", async (IFeatureManager features, CancellationToken ct) =>
{
    var flags = new SortedDictionary<string, bool>(StringComparer.Ordinal);
    await foreach (string name in features.GetFeatureNamesAsync().WithCancellation(ct))
    {
        flags[name] = await features.IsEnabledAsync(name);
    }

    return Results.Ok(flags);
}).WithTags("Platform");

app.MapSampleEndpoints();

await app.RunAsync();
return 0;

/// <summary>Entry point marker so WebApplicationFactory can boot the host in API tests.</summary>
public partial class Program;

internal static partial class MigrateLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Migrating module {Module}")]
    public static partial void Migrating(ILogger logger, string module);

    [LoggerMessage(Level = LogLevel.Information, Message = "All module migrations applied")]
    public static partial void Done(ILogger logger);
}
