using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordsController(IPasswordService passwords, IEmailVerificationService verification) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [AllowAnonymous, HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        await verification.RequestOtpAsync(new(request.Email, OtpPurpose.ResetPassword), ct);
        return Accepted(new VerificationAcceptedResponse());
    }

    [AllowAnonymous, HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await passwords.ResetPasswordAsync(request, ct);
        return NoContent();
    }

    [Authorize, HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await passwords.ChangePasswordAsync(UserId, request, ct);
        return NoContent();
    }
}
