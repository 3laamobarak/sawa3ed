using System.Diagnostics;

namespace Sawa3ed.Api.Observability;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        try { await next(context); }
        finally
        {
            logger.LogInformation("HTTP {Method} {Endpoint} returned {StatusCode} in {ElapsedMs} ms; trace {TraceId}",
                context.Request.Method, context.GetEndpoint()?.DisplayName ?? "unmatched", context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
        }
    }
}
