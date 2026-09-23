using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class RoleService(AppDbContext db, UserManager<ApplicationUser> users, IMemoryCache cache,
    TimeProvider clock, ILogger<RoleService> logger) : IRoleService
{
    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync("identity:roles:v1", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var names = await db.Roles.AsNoTracking().OrderBy(x => x.Name).Select(x => x.Name!).ToListAsync(ct);
            return names.Select(name => new RoleResponse(name, Permissions.ForRole(name))).ToArray();
        }))!;

    public async Task SetRoleAsync(string actorId, string userId, string role, bool remove, CancellationToken ct)
    {
        if (!Roles.All.Contains(role, StringComparer.Ordinal)) throw AppException.Invalid("Unknown role.");
        // Prevent accidental removal of the acting administrator's own access.
        if (remove && role == Roles.Admin && actorId == userId) throw AppException.Invalid("You cannot remove your own Admin role.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = await users.FindByIdAsync(userId) ?? throw AppException.NotFound();
        var hasRole = await users.IsInRoleAsync(user, role);
        if (remove == hasRole)
        {
            AuthService.Ensure(remove ? await users.RemoveFromRoleAsync(user, role) : await users.AddToRoleAsync(user, role));
            AuthService.Ensure(await users.UpdateSecurityStampAsync(user));
            var now = clock.GetUtcNow().UtcDateTime;
            await db.Sessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
        }
        await tx.CommitAsync(ct);
        logger.LogInformation("Role change ActorId={ActorId} TargetId={TargetId} Role={Role} Removed={Removed}", actorId, userId, role, remove);
    }
}
