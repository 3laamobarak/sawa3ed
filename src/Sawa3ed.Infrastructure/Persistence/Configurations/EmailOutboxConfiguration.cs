using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Infrastructure.Email;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class EmailOutboxConfiguration : IEntityTypeConfiguration<EmailOutbox>
{
    public void Configure(EntityTypeBuilder<EmailOutbox> e)
    {
        e.Property(x => x.To).HasMaxLength(254);
        e.Property(x => x.Subject).HasMaxLength(200);
        e.Property(x => x.ProtectedBody).HasMaxLength(4000);
        e.HasIndex(x => new { x.SentAtUtc, x.NextAttemptAtUtc });
    }
}
