using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Common;
using Sawa3ed.Domain.Chat;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class ChatRepository(AppDbContext db) : IChatRepository
{
    public Task<ChatConversation?> FindOwnedAsync(string ownerId, Guid id, CancellationToken ct) =>
        db.ChatConversations.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == ownerId, ct);

    public async Task<IReadOnlyList<ChatTurn>> RecentTurnsAsync(string ownerId, Guid conversationId, int limit, CancellationToken ct)
    {
        var turns = await db.ChatMessages.AsNoTracking()
            .Where(x => x.OwnerId == ownerId && x.ConversationId == conversationId)
            .OrderByDescending(x => x.Sequence).Take(limit)
            .Select(x => new ChatTurn(x.Role, x.Content)).ToListAsync(ct);
        turns.Reverse();
        return turns;
    }

    public async Task<Page<ChatMessageResponse>> PageMessagesAsync(string ownerId, Guid conversationId,
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.ChatMessages.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ConversationId == conversationId);
        var count = await query.CountAsync(ct);
        if (count == 0) throw AppException.NotFound();
        var items = await query.OrderBy(x => x.Sequence).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ChatMessageResponse(x.Id, x.Role, x.Content, x.CreatedAtUtc)).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }

    public void AddConversation(ChatConversation conversation) => db.ChatConversations.Add(conversation);
    public void AddMessage(ChatMessage message) => db.ChatMessages.Add(message);
    public void RemoveConversation(ChatConversation conversation) => db.ChatConversations.Remove(conversation);
}
