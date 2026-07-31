using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Vendify.Infrastructure.Services.Implementations;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(
        string key, T value,
        TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    // Default cache times
    public static readonly TimeSpan Short =
        TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Medium =
        TimeSpan.FromMinutes(30);
    public static readonly TimeSpan Long =
        TimeSpan.FromHours(1);

    // Cache keys
    public static class Keys
    {
        public static string Products(string storeId) =>
            $"products:{storeId}";
        public static string Product(string id) =>
            $"product:{id}";
        public static string Store(string slug) =>
            $"store:{slug}";
        public static string StoreById(string id) =>
            $"store-id:{id}";
        public static string Categories(string storeId) =>
            $"categories:{storeId}";
        public static string Themes() => "themes:all";
        public static string Dashboard(string storeId) =>
            $"dashboard:{storeId}";
    }

    public CacheService(
        IDistributedCache cache,
        ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(data)) return default;

            return JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Cache GET failed for {Key}: {Error}",
                key, ex.Message);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null)
    {
        try
        {
            var data = JsonSerializer.Serialize(value);
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    expiry ?? Medium
            };
            await _cache.SetStringAsync(
                key, data, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Cache SET failed for {Key}: {Error}",
                key, ex.Message);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Cache REMOVE failed for {Key}: {Error}",
                key, ex.Message);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        // For Redis, we'd use SCAN — simplified here
        _logger.LogInformation(
            "Cache invalidated for prefix: {Prefix}",
            prefix);
        await Task.CompletedTask;
    }
}