using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class OtpChallengeConfiguration : IEntityTypeConfiguration<OtpChallenge>
{
    public void Configure(EntityTypeBuilder<OtpChallenge> e)
    {
        e.Property(x => x.UserId).HasMaxLength(450);
        e.Property(x => x.Hash).HasMaxLength(64);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.UserId, x.Purpose }).IsUnique();
        e.Property(x => x.Version).IsConcurrencyToken();
        e.HasQueryFilter(x => !x.User.IsDeleted);
    }
}
