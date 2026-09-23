using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock)
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
    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public string HashOtp(Guid challengeId, string code) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(options.Value.OtpPepper), Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));
    public bool VerifyOtp(Guid challengeId, string code, string expectedHash) => CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(HashOtp(challengeId, code)), Convert.FromHexString(expectedHash));
}
