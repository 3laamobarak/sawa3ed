using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Application.Chat;

public interface IChatService
{
    Task<ChatResponse> SendAsync(string userId, ChatRequest request, CancellationToken ct);
    Task<Page<ChatMessageResponse>> HistoryAsync(string userId, Guid conversationId, int page, int pageSize, CancellationToken ct);
    Task DeleteAsync(string userId, Guid conversationId, CancellationToken ct);
}
