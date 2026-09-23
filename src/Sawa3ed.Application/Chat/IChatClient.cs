namespace Sawa3ed.Application.Chat;

public interface IChatClient
{
    Task<string> CompleteAsync(IReadOnlyList<ChatTurn> messages, CancellationToken ct);
}
