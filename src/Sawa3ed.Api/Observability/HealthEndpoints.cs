using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Api.Observability;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        endpoints.MapGet("/health/ready", async (IApplicationReadiness readiness, CancellationToken ct) =>
        {
            try { return await readiness.IsReadyAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503); }
            catch (Exception ex) when (ex is not OperationCanceledException) { return Results.StatusCode(503); }
        }).AllowAnonymous().RequireRateLimiting("api");
        return endpoints;
    }
}
