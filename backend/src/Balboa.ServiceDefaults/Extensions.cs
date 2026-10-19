using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Balboa.ServiceDefaults;

/// <summary>
/// Cross-cutting defaults every Balboa process gets: OpenTelemetry, health checks, HTTP resilience.
/// Exporters are opt-in through configuration, so nothing leaves the process unless asked to:
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> enables OTLP; <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> enables Azure Monitor.
/// </summary>
public static class Extensions
{
    public const string LiveTag = "live";
    public const string ReadyTag = "ready";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        // "self" proves the process is up and answering; readiness checks are added by modules with ReadyTag.
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), [LiveTag]);

        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        string? otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        string? azureMonitor = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        bool useOtlp = !string.IsNullOrWhiteSpace(otlpEndpoint);
        bool useAzureMonitor = !string.IsNullOrWhiteSpace(azureMonitor);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
            if (useOtlp)
            {
                logging.AddOtlpExporter();
            }

            if (useAzureMonitor)
            {
                logging.AddAzureMonitorLogExporter(o => o.ConnectionString = azureMonitor);
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                if (useOtlp)
                {
                    metrics.AddOtlpExporter();
                }

                if (useAzureMonitor)
                {
                    metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = azureMonitor);
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(o =>
                        // Health probes are noise in traces.
                        o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
                    .AddHttpClientInstrumentation();
                if (useOtlp)
                {
                    tracing.AddOtlpExporter();
                }

                if (useAzureMonitor)
                {
                    tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = azureMonitor);
                }
            });

        return builder;
    }

    /// <summary>Maps <c>/health/live</c> (process is up) and <c>/health/ready</c> (dependencies such as the database are reachable).</summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = r => r.Tags.Contains(LiveTag) });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains(ReadyTag) });
        return app;
    }
}
