using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> e)
    {
        e.Property(x => x.DisplayName).HasMaxLength(100);
        // SQLite cannot translate native DateTimeOffset ordering. UTC ticks are portable.
        e.Property(x => x.LockoutEnd).HasConversion(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
            value => value.UtcTicks, value => new DateTimeOffset(value, TimeSpan.Zero)));
        e.HasIndex(x => x.NormalizedEmail).IsUnique();
        e.HasQueryFilter(x => !x.IsDeleted);
    }
}
