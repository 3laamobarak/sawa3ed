using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Common;
using Sawa3ed.Domain.Chat;

namespace Sawa3ed.Application.Chat;

public sealed class ChatService(IChatRepository chats, IUnitOfWork unitOfWork,
    IChatClient client, IChatContextBuilder contextBuilder) : IChatService
{
    public async Task<ChatResponse> SendAsync(string userId, ChatRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
            throw AppException.Invalid("A message of 1–4000 characters is required.");
        var conversation = request.ConversationId is { } existingId
            ? await chats.FindOwnedAsync(userId, existingId, ct) ?? throw AppException.NotFound()
            : new ChatConversation { OwnerId = userId };
        var history = await chats.RecentTurnsAsync(userId, conversation.Id, contextBuilder.HistoryLimit, ct);
        var sequence = conversation.LastSequence;
        var turns = contextBuilder.Build(history, request.Message);

        // Do not hold a transaction while waiting for the paid provider call.
        var reply = await client.CompleteAsync(turns, ct);
        if (request.ConversationId is null) chats.AddConversation(conversation);
        conversation.LastSequence = sequence + 2;
        chats.AddMessage(new ChatMessage
        {
            OwnerId = userId, ConversationId = conversation.Id,
            Role = "user", Content = request.Message, Sequence = sequence + 1
        });
        chats.AddMessage(new ChatMessage
        {
            OwnerId = userId, ConversationId = conversation.Id,
            Role = "assistant", Content = reply, Sequence = sequence + 2
        });
        // One atomic save; the aggregate version rejects concurrent sends/deletion.
        await unitOfWork.SaveChangesAsync(ct);
        return new(conversation.Id, reply);
    }

    public Task<Page<ChatMessageResponse>> HistoryAsync(string userId, Guid conversationId,
        int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw AppException.Invalid("Invalid pagination.");
        return chats.PageMessagesAsync(userId, conversationId, page, pageSize, ct);
    }

    public async Task DeleteAsync(string userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = await chats.FindOwnedAsync(userId, conversationId, ct) ?? throw AppException.NotFound();
        chats.RemoveConversation(conversation);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
