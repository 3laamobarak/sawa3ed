using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : IAccessTokenIssuer
{
    public TokenResponse Issue(ApplicationUser user, AuthSession session, IList<string> roles, string refresh)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(settings.AccessMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("sid", session.Id.ToString()),
            new("sst", session.SecurityStamp)
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(roles.SelectMany(Permissions.ForRole).Distinct().Select(permission => new Claim("permission", permission)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires, credentials);
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, refresh, session.ExpiresAtUtc);
    }
}
