using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAuthService auth) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;
    private static object AcceptedMessage => new { message = "If the account is eligible, a verification email will arrive shortly." };
    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await auth.RegisterAsync(request, ct);
        return Accepted(AcceptedMessage);
    }
    [AllowAnonymous, HttpPost("login")]
    public Task<TokenResponse> Login(LoginRequest request, CancellationToken ct) => auth.LoginAsync(request, ct);
    [AllowAnonymous, HttpPost("refresh")]
    public Task<TokenResponse> Refresh(RefreshRequest request, CancellationToken ct) => auth.RefreshAsync(request.RefreshToken, ct);
    [AllowAnonymous, HttpPost("otp/request")]
    public async Task<IActionResult> RequestOtp(OtpRequest request, CancellationToken ct)
    {
        await auth.RequestOtpAsync(request, ct);
        return Accepted(AcceptedMessage);
    }
    [AllowAnonymous, HttpPost("email/confirm")]
    public async Task<IActionResult> ConfirmEmail(VerifyEmailRequest request, CancellationToken ct)
    {
        await auth.ConfirmEmailAsync(request, ct);
        return NoContent();
    }
    [AllowAnonymous, HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword(EmailRequest request, CancellationToken ct)
    {
        await auth.RequestOtpAsync(new(request.Email, OtpPurpose.ResetPassword), ct);
        return Accepted(AcceptedMessage);
    }
    [AllowAnonymous, HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken ct)
    {
        await auth.ResetPasswordAsync(request, ct);
        return NoContent();
    }
    [Authorize, HttpPost("password/change")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(UserId, request, ct);
        return NoContent();
    }
    [Authorize, HttpGet("me"), EnableRateLimiting("api")]
    public Task<UserResponse> Me(CancellationToken ct) => auth.MeAsync(UserId, ct);
    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(UserId, Guid.Parse(User.FindFirst("sid")!.Value), false, ct);
        return NoContent();
    }
    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await auth.LogoutAsync(UserId, Guid.Parse(User.FindFirst("sid")!.Value), true, ct);
        return NoContent();
    }
}
public sealed record EmailRequest([System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.EmailAddress,
    System.ComponentModel.DataAnnotations.MaxLength(254)] string Email);
