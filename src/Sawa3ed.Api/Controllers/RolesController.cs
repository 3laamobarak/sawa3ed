using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Api.Controllers;

[ApiController, Route("api/v1/roles")]
[Authorize(Policy = Permissions.ManageRoles)]
[EnableRateLimiting("api")]
public sealed class RolesController(IRoleService roles) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<RoleResponse>> List(CancellationToken ct) => roles.ListAsync(ct);
    [HttpPost("users/{userId}")]
    public async Task<IActionResult> Assign(string userId, RoleAssignmentRequest request, CancellationToken ct)
    {
        await roles.SetRoleAsync(User.FindFirst("sub")!.Value, userId, request.Role, false, ct);
        return NoContent();
    }
    [HttpDelete("users/{userId}/{role}")]
    public async Task<IActionResult> Remove(string userId, string role, CancellationToken ct)
    {
        await roles.SetRoleAsync(User.FindFirst("sub")!.Value, userId, role, true, ct);
        return NoContent();
    }
}
