namespace Sawa3ed.Application.Auth;

public interface IAuthSessionService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<TokenResponse> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string userId, Guid sessionId, bool allSessions, CancellationToken ct);
}
