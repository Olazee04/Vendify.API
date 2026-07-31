using Serilog;
using Serilog.Events;

namespace Vendify.API.Configuration;

public static class SerilogConfig
{
    public static void ConfigureSerilog(
        this WebApplicationBuilder builder)
    {
        var logPath = Path.Combine("logs", "vendify-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override(
                "Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override(
                "Microsoft.Hosting.Lifetime",
                LogEventLevel.Information)
            .MinimumLevel.Override(
                "System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] " +
                "{Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate:
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} " +
                "[{Level:u3}] {Message:lj}" +
                "{NewLine}{Exception}")
            .CreateLogger();

        builder.Host.UseSerilog();
    }
}
