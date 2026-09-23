using System.ComponentModel.DataAnnotations;
using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Application.Chat;

public sealed record ChatRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Message,
    Guid? ConversationId = null);
public sealed record ChatTurn(string Role, string Content);
public sealed record ChatResponse(Guid ConversationId, string Reply);
public sealed record ChatMessageResponse(Guid Id, string Role, string Content, DateTime CreatedAtUtc);
public interface IChatClient
{
    Task<string> CompleteAsync(IReadOnlyList<ChatTurn> messages, CancellationToken ct);
}
public interface IChatService
{
    Task<ChatResponse> SendAsync(string userId, ChatRequest request, CancellationToken ct);
    Task<Page<ChatMessageResponse>> HistoryAsync(string userId, Guid conversationId, int page, int pageSize, CancellationToken ct);
    Task DeleteAsync(string userId, Guid conversationId, CancellationToken ct);
}
