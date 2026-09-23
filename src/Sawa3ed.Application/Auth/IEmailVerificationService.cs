namespace Sawa3ed.Application.Auth;

public interface IEmailVerificationService
{
    Task RequestOtpAsync(OtpRequest request, CancellationToken ct);
    Task ConfirmEmailAsync(VerifyEmailRequest request, CancellationToken ct);
}
