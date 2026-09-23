using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthSessionsController(IAuthSessionService sessions) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [AllowAnonymous, HttpPost("login")]
    public Task<TokenResponse> Login(LoginRequest request, CancellationToken ct) => sessions.LoginAsync(request, ct);

    [AllowAnonymous, HttpPost("refresh")]
    public Task<TokenResponse> Refresh(RefreshRequest request, CancellationToken ct) => sessions.RefreshAsync(request.RefreshToken, ct);

    [Authorize, HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await sessions.LogoutAsync(UserId, Guid.Parse(User.FindFirst("sid")!.Value), false, ct);
        return NoContent();
    }

    [Authorize, HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        await sessions.LogoutAsync(UserId, Guid.Parse(User.FindFirst("sid")!.Value), true, ct);
        return NoContent();
    }
}
