namespace Sawa3ed.Application.Chat;

public interface IChatContextBuilder
{
    int HistoryLimit { get; }
    IReadOnlyList<ChatTurn> Build(IReadOnlyList<ChatTurn> history, string message);
}
