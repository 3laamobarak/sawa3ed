using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Identity;

internal sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> settings) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions o)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme) return;
        var jwt = settings.Value;
        o.MapInboundClaims = false;
        o.RequireHttpsMetadata = true;
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(15),
            NameClaimType = "sub", RoleClaimType = "role"
        };
        o.EventsType = typeof(SessionValidationEvents);
    }
}
