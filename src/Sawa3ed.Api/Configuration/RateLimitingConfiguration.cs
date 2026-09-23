using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Api.Security;

namespace Sawa3ed.Api.Configuration;

internal static class RateLimitingConfiguration
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = 429;
            // IP admission happens in middleware before authentication DB lookups as well.
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetConcurrencyLimiter("server", _ => new ConcurrencyLimiterOptions { PermitLimit = 100, QueueLimit = 0 }));
            foreach (var (name, limit) in new[] { ("auth", 20), ("api", 120), ("upload", 10), ("chat", 10) })
            {
                o.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                    name == "auth" ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown" : context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
            }
            o.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                await Results.Problem(statusCode: 429, title: "rate_limit_exceeded", detail: "Too many requests. Try again shortly.").ExecuteAsync(context.HttpContext);
            };
        });
        services.AddSingleton<AdmissionLimiter>();
        return services;
    }
}
