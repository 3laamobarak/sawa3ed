using Sawa3ed.Domain.Common;

namespace Sawa3ed.Domain.Chat;

public sealed class ChatConversation : Entity
{
    public string OwnerId { get; set; } = "";
    public long LastSequence { get; set; }
}
