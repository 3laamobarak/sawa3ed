namespace Sawa3ed.Application.Auth;

public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc);
