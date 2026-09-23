using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountsController(IAccountService accounts) : ControllerBase
{
    private string UserId => User.FindFirst("sub")!.Value;

    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await accounts.RegisterAsync(request, ct);
        return Accepted(new VerificationAcceptedResponse());
    }

    [Authorize, HttpGet("me"), EnableRateLimiting("api")]
    public Task<UserResponse> Me(CancellationToken ct) => accounts.MeAsync(UserId, ct);
}
