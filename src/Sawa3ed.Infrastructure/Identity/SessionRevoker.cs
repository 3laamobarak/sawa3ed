using Microsoft.EntityFrameworkCore;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class SessionRevoker(AppDbContext db, TimeProvider clock) : ISessionRevoker
{
    public async Task RevokeAsync(string userId, Guid? sessionId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await db.Sessions.Where(x => x.UserId == userId && x.RevokedAtUtc == null &&
                (sessionId == null || x.Id == sessionId))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, now), ct);
    }
}
