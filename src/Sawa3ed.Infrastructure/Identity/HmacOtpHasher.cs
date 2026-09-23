using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class HmacOtpHasher(IOptions<JwtOptions> options) : IOtpHasher
{
    public string Hash(Guid challengeId, string code) => Convert.ToHexString(HMACSHA256.HashData(
        Encoding.UTF8.GetBytes(options.Value.OtpPepper), Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));
    public bool Verify(Guid challengeId, string code, string expectedHash) => CryptographicOperations.FixedTimeEquals(
        Convert.FromHexString(Hash(challengeId, code)), Convert.FromHexString(expectedHash));
}
