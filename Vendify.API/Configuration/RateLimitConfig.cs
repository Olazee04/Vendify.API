using AspNetCoreRateLimit;

namespace Vendify.API.Configuration;

public static class RateLimitConfig
{
    public static IServiceCollection AddVendifyRateLimiting(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.AddMemoryCache();

        services.Configure<IpRateLimitOptions>(
            config.GetSection("IpRateLimiting"));

        services.Configure<ClientRateLimitOptions>(
            config.GetSection("ClientRateLimiting"));

        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();

        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();

        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();

        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

        services.AddInMemoryRateLimiting();

        return services;
    }
}
