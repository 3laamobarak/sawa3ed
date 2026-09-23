using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> e)
    {
        e.Property(x => x.Hash).HasMaxLength(64);
        e.HasIndex(x => x.Hash).IsUnique();
        e.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasQueryFilter(x => !x.Session.User.IsDeleted);
    }
}
