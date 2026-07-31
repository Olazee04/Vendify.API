using System.Diagnostics;

namespace Vendify.API.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware>
        _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var requestId = Guid.NewGuid()
            .ToString()[..8].ToUpper();

        // Log request
        _logger.LogInformation(
            "→ [{RequestId}] {Method} {Path} " +
            "from {IP}",
            requestId,
            context.Request.Method,
            context.Request.Path,
            context.Connection.RemoteIpAddress);

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var level = context.Response.StatusCode >= 500
                ? Microsoft.Extensions.Logging
                    .LogLevel.Error
                : context.Response.StatusCode >= 400
                    ? Microsoft.Extensions.Logging
                        .LogLevel.Warning
                    : Microsoft.Extensions.Logging
                        .LogLevel.Information;

            _logger.Log(level,
                "← [{RequestId}] {StatusCode} " +
                "{Method} {Path} in {ElapsedMs}ms",
                requestId,
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path,
                sw.ElapsedMilliseconds);
        }
    }
}