using Sawa3ed.Application.Chat;

namespace Sawa3ed.Tests;

public sealed class ChatContextBuilderTests
{
    [Fact]
    public void Trimming_drops_orphaned_assistant_reply_and_preserves_roles()
    {
        var builder = new ChatContextBuilder(new(3, 100));
        ChatTurn[] history = [new("user", "old"), new("assistant", "old reply"), new("user", "recent"), new("assistant", "recent reply")];

        var turns = builder.Build(history, "next");

        Assert.Equal(new[] { "system", "user", "assistant", "user" }, turns.Select(x => x.Role));
        Assert.Equal(new[] { "recent", "recent reply", "next" }, turns.Skip(1).Select(x => x.Content));
        Assert.Equal(4, history.Length);
    }

    [Fact]
    public void Character_budget_discards_old_pairs_without_truncating_the_new_question()
    {
        var builder = new ChatContextBuilder(new(12, 10));
        ChatTurn[] history = [new("user", "12345678"), new("assistant", "12345678")];

        var turns = builder.Build(history, "question");

        Assert.Equal(new[] { "system", "user" }, turns.Select(x => x.Role));
        Assert.Equal("question", turns[^1].Content);
    }
}
