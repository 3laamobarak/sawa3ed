using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Domain.Chat;
using Sawa3ed.Domain.Common;
using Sawa3ed.Domain.Files;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Email;

namespace Sawa3ed.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions options, TimeProvider clock, ICurrentUser actor)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuthSession> Sessions => Set<AuthSession>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<StoredFile> Files => Set<StoredFile>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<EmailOutbox>(e =>
        {
            e.Property(x => x.To).HasMaxLength(254);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.ProtectedBody).HasMaxLength(4000);
            e.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc });
        });
        model.Entity<ApplicationUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100);
            // SQLite cannot translate native DateTimeOffset ordering. UTC ticks are portable.
            e.Property(x => x.LockoutEnd).HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
                value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero)));
            e.HasIndex(x => x.NormalizedEmail).IsUnique();
            e.HasQueryFilter(x => !x.IsDeleted);
        });
        model.Entity<AuthSession>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.SecurityStamp).HasMaxLength(100);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
            e.HasQueryFilter(x => !x.User.IsDeleted);
        });
        model.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasIndex(x => x.Hash).IsUnique();
            e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasQueryFilter(x => !x.Session.User.IsDeleted);
        });
        model.Entity<OtpChallenge>(e =>
        {
            e.Property(x => x.UserId).HasMaxLength(450);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.UserId, x.Purpose }).IsUnique();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasQueryFilter(x => !x.User.IsDeleted);
        });
        model.Entity<StoredFile>(e =>
        {
            e.Property(x => x.OwnerId).HasMaxLength(450);
            e.Property(x => x.StorageKey).HasMaxLength(64);
            e.Property(x => x.OriginalName).HasMaxLength(255);
            e.Property(x => x.Folder).HasMaxLength(500);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasIndex(x => x.StorageKey).IsUnique();
            e.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.CreatedAtUtc, x.Id });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ChatConversation>(e =>
        {
            e.Property(x => x.OwnerId).HasMaxLength(450);
            e.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.CreatedAtUtc });
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.OwnerId).HasMaxLength(450);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.Content).HasMaxLength(16000);
            e.HasIndex(x => new { x.OwnerId, x.ConversationId, x.IsDeleted, x.Sequence });
            e.HasIndex(x => new { x.OwnerId, x.ConversationId, x.Sequence }).IsUnique();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
        });
        // Applies automatically to every future domain Entity, not a hand-maintained list.
        foreach (var type in model.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            var e = model.Entity(type.ClrType);
            var parameter = Expression.Parameter(type.ClrType, "entity");
            e.HasQueryFilter(Expression.Lambda(Expression.Not(Expression.Property(parameter, nameof(Entity.IsDeleted))), parameter));
            e.Property(nameof(Entity.Version)).IsConcurrencyToken();
            e.Property(nameof(Entity.CreatedBy)).HasMaxLength(450);
            e.Property(nameof(Entity.UpdatedBy)).HasMaxLength(450);
            e.Property(nameof(Entity.DeletedBy)).HasMaxLength(450);
        }
        model.Entity<ChatMessage>().HasQueryFilter(x => !x.IsDeleted && !x.Conversation.IsDeleted);
    }

    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareChanges()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedBy = actor.UserId;
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
                    entry.Entity.DeletedBy = actor.UserId;
                }
                entry.Entity.UpdatedAtUtc = now;
                entry.Entity.UpdatedBy = actor.UserId;
                entry.Entity.Version = Guid.NewGuid();
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
            }
        }
        foreach (var entry in ChangeTracker.Entries<ApplicationUser>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.SecurityStamp = Guid.NewGuid().ToString();
        }
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Modified))
        {
            if (entry.Entity is AuthSession session) session.Version = Guid.NewGuid();
            if (entry.Entity is RefreshToken token) token.Version = Guid.NewGuid();
            if (entry.Entity is OtpChallenge otp) otp.Version = Guid.NewGuid();
        }
    }
}
