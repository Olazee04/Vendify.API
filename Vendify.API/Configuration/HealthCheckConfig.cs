using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Vendify.API.Configuration;

public static class HealthCheckConfig
{
    public static IServiceCollection AddVendifyHealthChecks(
        this IServiceCollection services,
        IConfiguration config)
    {
        var connectionString = config
            .GetConnectionString("DefaultConnection") ?? "";

        services.AddHealthChecks()
            .AddNpgSql(
                connectionString,
                name: "PostgreSQL",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "database", "postgres" })
            .AddCheck("API",
                () => HealthCheckResult.Healthy("API is running"),
                tags: new[] { "api" })
            .AddCheck<CloudinaryHealthCheck>(
                "Cloudinary",
                tags: new[] { "storage" });

        return services;
    }

    public static WebApplication UseVendifyHealthChecks(
        this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var result = new
                {
                    status = report.Status.ToString(),
                    timestamp = DateTime.UtcNow,
                    version = "1.0.0",
                    environment = app.Environment.EnvironmentName,
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString(),
                        description = e.Value.Description,
                        duration = e.Value.Duration.TotalMilliseconds
                    })
                };
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(result,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        }));
            }
        });

        app.MapHealthChecks("/health/detail", new HealthCheckOptions
        {
            ResponseWriter =
                HealthChecks.UI.Client.UIResponseWriter
                    .WriteHealthCheckUIResponse
        });

        return app;
    }
}

public class CloudinaryHealthCheck : IHealthCheck
{
    private readonly IConfiguration _config;

    public CloudinaryHealthCheck(IConfiguration config)
    {
        _config = config;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cloudName = _config["Cloudinary:CloudName"];
        return Task.FromResult(
            !string.IsNullOrEmpty(cloudName)
                ? HealthCheckResult.Healthy("Cloudinary configured")
                : HealthCheckResult.Degraded("Cloudinary not configured"));
    }
}
