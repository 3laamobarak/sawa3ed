using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Api.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirst("sub")?.Value;
}
