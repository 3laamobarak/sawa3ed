using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Domain.Files;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> e)
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
    }
}
