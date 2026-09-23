using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Domain.Chat;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> e)
    {
        e.Property(x => x.OwnerId).HasMaxLength(450);
        e.HasIndex(x => new { x.OwnerId, x.IsDeleted, x.CreatedAtUtc });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
    }
}
