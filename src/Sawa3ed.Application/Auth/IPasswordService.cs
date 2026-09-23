namespace Sawa3ed.Application.Auth;

public interface IPasswordService
{
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct);
}
