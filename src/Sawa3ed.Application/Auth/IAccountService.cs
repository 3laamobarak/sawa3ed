namespace Sawa3ed.Application.Auth;

public interface IAccountService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<UserResponse> MeAsync(string userId, CancellationToken ct);
}
