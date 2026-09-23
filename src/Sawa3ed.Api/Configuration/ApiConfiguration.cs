using System.Diagnostics;
using Sawa3ed.Api.Errors;
using Sawa3ed.Api.Security;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Files;

namespace Sawa3ed.Api.Configuration;

internal static class ApiConfiguration
{
    public static WebApplicationBuilder AddApi(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.UseUtcTimestamp = true; o.TimestampFormat = "O"; });
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();
        builder.Services.AddScoped<IFileService, FileService>();
        builder.Services.AddScoped<IChatService, ChatService>();
        builder.Services.AddSingleton<IChatContextBuilder, ChatContextBuilder>();
        builder.Services.AddControllers().AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            o.JsonSerializerOptions.MaxDepth = 32;
        });
        builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.WebHost.ConfigureKestrel(o =>
        {
            o.AddServerHeader = false;
            o.Limits.MaxRequestBodySize = 1_048_576;
            o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddHttpSecurity(builder.Configuration);
        builder.Services.AddApiRateLimiting(builder.Configuration);
        builder.Services.AddApiDocumentation(builder.Configuration);
        return builder;
    }
}
