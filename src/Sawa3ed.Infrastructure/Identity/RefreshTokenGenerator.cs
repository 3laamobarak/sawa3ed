using System.Security.Cryptography;
using System.Text;

namespace Sawa3ed.Infrastructure.Identity;

internal static class RefreshTokenGenerator
{
    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
