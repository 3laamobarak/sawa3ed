using Sawa3ed.Application.Abstractions;
using Sawa3ed.Domain.Chat;

namespace Sawa3ed.Application.Chat;

public interface IChatRepository
{
    // Return a tracked aggregate so its version protects a later save from races.
    Task<ChatConversation?> FindOwnedAsync(string ownerId, Guid id, CancellationToken ct);
    // Return at most limit turns in chronological order, without tracking messages.
    Task<IReadOnlyList<ChatTurn>> RecentTurnsAsync(string ownerId, Guid conversationId, int limit, CancellationToken ct);
    Task<Page<ChatMessageResponse>> PageMessagesAsync(string ownerId, Guid conversationId, int page, int pageSize, CancellationToken ct);
    void AddConversation(ChatConversation conversation);
    void AddMessage(ChatMessage message);
    void RemoveConversation(ChatConversation conversation);
}
