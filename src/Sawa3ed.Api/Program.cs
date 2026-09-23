using System.Diagnostics;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Sawa3ed.Api.Errors;
using Sawa3ed.Api.Security;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; o.TimestampFormat = "O"; });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    o.JsonSerializerOptions.MaxDepth = 32;
});
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    foreach (var permission in new[] { Permissions.ManageRoles, Permissions.UploadFiles, Permissions.Chat })
        o.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim("permission", permission));
});
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
{
    var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET", "POST", "DELETE").WithHeaders("Content-Type", "Authorization");
}));
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var address in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        o.KnownProxies.Add(IPAddress.Parse(address));
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 1_048_576;
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddRateLimiter(o =>
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
builder.Services.AddSingleton<AdmissionLimiter>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Sawa3ed Foundation API", Version = "v1", Description = "Identity, private files, learning assistant, and health. Business-module endpoints are intentionally excluded." });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Paste only the accessToken from POST /api/v1/auth/login."
    });
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    if (args.Contains("--migrate")) { await initializer.MigrateAsync(CancellationToken.None); return; }
    if (args.Contains("--seed-admin")) { await initializer.SeedAdminAsync(CancellationToken.None); return; }
    if (app.Environment.IsDevelopment()) await initializer.MigrateAsync(CancellationToken.None);
}
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
    await Results.Problem(statusCode: context.HttpContext.Response.StatusCode).ExecuteAsync(context.HttpContext));
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Trace-Id"] = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    var started = Stopwatch.GetTimestamp();
    try { await next(); }
    finally
    {
        app.Logger.LogInformation("HTTP {Method} {Endpoint} returned {StatusCode} in {ElapsedMs} ms; trace {TraceId}",
            context.Request.Method, context.GetEndpoint()?.DisplayName ?? "unmatched", context.Response.StatusCode,
            Stopwatch.GetElapsedTime(started).TotalMilliseconds, context.TraceIdentifier);
    }
});
app.UseRouting();
app.UseCors();
app.UseMiddleware<AdmissionMiddleware>();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "Sawa3ed v1"); o.EnablePersistAuthorization(); });
}
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    try { return await db.Roles.AsNoTracking().AnyAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503); }
    catch (Exception ex) when (ex is not OperationCanceledException) { return Results.StatusCode(503); }
}).AllowAnonymous().RequireRateLimiting("api");
app.MapControllers();
await app.RunAsync();

public partial class Program;
