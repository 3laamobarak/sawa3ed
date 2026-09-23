using System.Threading.RateLimiting;

namespace Sawa3ed.Api.Security;

public sealed class AdmissionLimiter : IDisposable
{
    public PartitionedRateLimiter<HttpContext> Limiter { get; } = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    public void Dispose() => Limiter.Dispose();
}
