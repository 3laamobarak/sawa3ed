using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class SessionValidationEvents(AppDbContext db, TimeProvider clock) : JwtBearerEvents
{
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        var principal = context.Principal!;
        var userId = principal.FindFirst("sub")?.Value;
        var stamp = principal.FindFirst("sst")?.Value;
        if (!Guid.TryParse(principal.FindFirst("sid")?.Value, out var sid)) { context.Fail("Invalid session."); return; }
        var now = clock.GetUtcNow().UtcDateTime;
        var nowOffset = clock.GetUtcNow();
        // One indexed projection. Deliberately not cached: logout/role changes take effect immediately.
        var valid = await db.Sessions.AsNoTracking().AnyAsync(x => x.Id == sid && x.UserId == userId &&
            x.RevokedAtUtc == null && x.ExpiresAtUtc > now && x.SecurityStamp == stamp &&
            x.User.SecurityStamp == stamp && x.User.EmailConfirmed &&
            (x.User.LockoutEnd == null || x.User.LockoutEnd <= nowOffset), context.HttpContext.RequestAborted);
        if (!valid) context.Fail("Session expired or revoked.");
    }
}
