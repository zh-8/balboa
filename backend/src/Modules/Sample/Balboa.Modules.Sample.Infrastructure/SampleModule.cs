using Balboa.BuildingBlocks.DependencyInjection;
using Balboa.BuildingBlocks.Messaging;
using Balboa.BuildingBlocks.Outbox;
using Balboa.BuildingBlocks.Persistence;
using Balboa.Modules.Sample.Application;
using Balboa.Modules.Sample.Contracts;
using Balboa.Modules.Sample.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Balboa.Modules.Sample.Infrastructure;

internal static class HealthTags
{
    // Must equal Balboa.ServiceDefaults.Extensions.ReadyTag (not referenced, to keep modules free of host concerns).
    public const string Ready = "ready";
}

/// <summary>The ONLY public entry point of the Sample module's Infrastructure assembly.</summary>
public static class SampleModule
{
    public const string ConnectionStringName = "balboa";

    public static IServiceCollection AddSampleModule(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured (ConnectionStrings:{ConnectionStringName}).");

        services.AddDbContext<SampleDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable(SampleDbContext.MigrationsHistoryTable, SampleDbContext.Schema)));

        services.AddBuildingBlocks();
        services.AddHandlersFromAssembly(typeof(ISampleUnitOfWork).Assembly);

        services.AddScoped<IWidgetCatalogRepository, WidgetCatalogRepository>();
        services.AddScoped<ISampleUnitOfWork, SampleUnitOfWork>();
        services.AddScoped<ISampleOutbox, SampleOutbox>();
        services.AddScoped<OutboxProcessor<SampleDbContext>>();
        services.AddScoped<IOutboxProcessor>(sp => sp.GetRequiredService<OutboxProcessor<SampleDbContext>>());
        services.AddScoped<ISampleQueries, SampleQueries>();
        services.AddScoped<IModuleMigrator>(sp =>
            new EfModuleMigrator<SampleDbContext>(sp.GetRequiredService<SampleDbContext>(), "sample"));

        services.AddHealthChecks().AddDbContextCheck<SampleDbContext>("sample-db", tags: [HealthTags.Ready]);
        return services;
    }

    public static IEndpointRouteBuilder MapSampleEndpoints(this IEndpointRouteBuilder app)
    {
        SampleEndpoints.Map(app);
        return app;
    }
}
