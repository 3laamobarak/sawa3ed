using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Sawa3ed.Domain.Common;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence;

internal static class AuditChangeProcessor
{
    public static void Apply(ChangeTracker tracker, DateTime now, string? actorId)
    {
        foreach (var entry in tracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = actorId;
                entry.Entity.IsDeleted = false;
                entry.Entity.DeletedAtUtc = null;
                entry.Entity.DeletedBy = null;
                entry.Entity.Version = Guid.NewGuid();
            }
            else if (entry.State is EntityState.Deleted or EntityState.Modified)
            {
                if (entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Unchanged;
                    entry.Entity.IsDeleted = true;
                }
                if (entry.Entity.IsDeleted && !entry.Property(x => x.IsDeleted).OriginalValue)
                {
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.DeletedBy = actorId;
                }
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = actorId;
                entry.Entity.Version = Guid.NewGuid();
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
            }
        }
        foreach (var entry in tracker.Entries<ApplicationUser>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.SecurityStamp = Guid.NewGuid().ToString();
        }
        foreach (var entry in tracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is AuthSession session) session.Version = Guid.NewGuid();
            if (entry.Entity is RefreshToken token) token.Version = Guid.NewGuid();
            if (entry.Entity is OtpChallenge otp) otp.Version = Guid.NewGuid();
        }
    }
}
