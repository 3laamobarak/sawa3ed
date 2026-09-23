using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sawa3ed.Domain.Chat;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence.Configurations;

internal sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> e)
    {
        e.Property(x => x.OwnerId).HasMaxLength(450);
        e.Property(x => x.Role).HasMaxLength(20);
        e.Property(x => x.Content).HasMaxLength(16000);
        e.HasIndex(x => new { x.OwnerId, x.ConversationId, x.IsDeleted, x.Sequence });
        e.HasIndex(x => new { x.OwnerId, x.ConversationId, x.Sequence }).IsUnique();
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Conversation).WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Restrict);
    }
}
