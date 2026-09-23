using System.Threading.RateLimiting;

namespace Sawa3ed.Api.Security;

public sealed class AdmissionLimiter : IDisposable
{
    public PartitionedRateLimiter<HttpContext> Limiter { get; } = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    public void Dispose() => Limiter.Dispose();
}
public sealed class AdmissionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AdmissionLimiter limiter)
    {
        using var lease = await limiter.Limiter.AcquireAsync(context, 1, context.RequestAborted);
        if (!lease.IsAcquired)
        {
            context.Response.Headers.RetryAfter = "60";
            await Results.Problem(statusCode: 429, title: "rate_limit_exceeded").ExecuteAsync(context);
            return;
        }
        await next(context);
    }
}
