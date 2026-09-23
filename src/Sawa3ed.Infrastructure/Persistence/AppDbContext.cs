using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Domain.Chat;
using Sawa3ed.Domain.Files;
using Sawa3ed.Infrastructure.Identity;

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
        model.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        model.ApplyEntityConventions();
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

    private void PrepareChanges() => AuditChangeProcessor.Apply(ChangeTracker, clock.GetUtcNow().UtcDateTime, actor.UserId);
}
