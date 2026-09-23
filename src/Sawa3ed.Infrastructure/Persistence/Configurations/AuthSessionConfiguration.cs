using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class AuthSessionConfiguration : IEntityTypeConfiguration<AuthSession>
{
    public void Configure(EntityTypeBuilder<AuthSession> e)
    {
        e.Property(x => x.UserId).HasMaxLength(450);
        e.Property(x => x.SecurityStamp).HasMaxLength(100);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        e.HasQueryFilter(x => !x.User.IsDeleted);
    }
}
