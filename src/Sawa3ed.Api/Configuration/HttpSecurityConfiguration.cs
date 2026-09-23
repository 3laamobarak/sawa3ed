using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Configuration;

internal static class HttpSecurityConfiguration
{
    public static IServiceCollection AddHttpSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthorization(o =>
        {
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permission in new[] { Permissions.ManageRoles, Permissions.UploadFiles, Permissions.Chat })
                o.AddPolicy(permission, p => p.RequireAuthenticatedUser().RequireClaim("permission", permission));
        });
        services.AddCors(o => o.AddDefaultPolicy(policy =>
        {
            var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
            if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET", "POST", "DELETE").WithHeaders("Content-Type", "Authorization");
        }));
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            foreach (var address in configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(IPAddress.Parse(address));
        });
        return services;
    }
}
