using Sawa3ed.Domain.Common;

namespace Sawa3ed.Domain.Chat;

public sealed class ChatMessage : Entity
{
    public string OwnerId { get; set; } = "";
    public Guid ConversationId { get; set; }
    public ChatConversation Conversation { get; set; } = null!;
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public long Sequence { get; set; }
}
