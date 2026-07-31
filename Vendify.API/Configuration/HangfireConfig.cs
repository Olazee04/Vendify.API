using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Vendify.Infrastructure.Jobs;

namespace Vendify.API.Configuration;

public static class HangfireConfig
{
    public static IServiceCollection AddVendifyHangfire(
        this IServiceCollection services,
        IConfiguration config)
    {
        var connectionString = config
            .GetConnectionString("DefaultConnection") ?? "";

        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(c =>
                c.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.Queues = new[] { "critical", "default", "low" };
        });

        services.AddScoped<OrderReminderJob>();

        return services;
    }

    public static WebApplication UseVendifyHangfire(
        this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseHangfireDashboard("/hangfire");
        }
        else
        {
            app.UseHangfireDashboard("/hangfire",
                new DashboardOptions
                {
                    Authorization = new[] { new HangfireAuthFilter() }
                });
        }

        RecurringJob.AddOrUpdate<OrderReminderJob>(
            "pending-order-reminders",
            job => job.RemindPendingOrdersAsync(),
            "*/30 * * * *");

        RecurringJob.AddOrUpdate<OrderReminderJob>(
            "low-stock-alerts",
            job => job.LowStockAlertsAsync(),
            Cron.Daily(8, 0));

        RecurringJob.AddOrUpdate<OrderReminderJob>(
            "weekly-reports",
            job => job.WeeklyReportAsync(),
            Cron.Weekly(DayOfWeek.Monday, 9, 0));

        RecurringJob.AddOrUpdate<OrderReminderJob>(
            "process-webhooks",
            job => job.ProcessPendingWebhooksAsync(),
            "*/5 * * * *");

        return app;
    }
}

public class HangfireAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
