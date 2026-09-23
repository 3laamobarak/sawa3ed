using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EmailVerificationController(IEmailVerificationService verification) : ControllerBase
{
    [AllowAnonymous, HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(OtpRequest request, CancellationToken ct)
    {
        await verification.RequestOtpAsync(request, ct);
        return Accepted(new VerificationAcceptedResponse());
    }

    [AllowAnonymous, HttpPost("email/confirm")]
    public async Task<IActionResult> ConfirmEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await verification.ConfirmEmailAsync(request, ct);
        return NoContent();
    }
}
