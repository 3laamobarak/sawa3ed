using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Common;

namespace Sawa3ed.Tests;

public sealed class FakeChat : IChatClient
{
    public IReadOnlyList<ChatTurn> LastMessages { get; private set; } = [];
    public bool Fail { get; set; }
    public Func<CancellationToken, Task>? BeforeReply { get; set; }
    public async Task<string> CompleteAsync(IReadOnlyList<ChatTurn> messages, CancellationToken ct)
    {
        LastMessages = messages.ToArray();
        if (Fail) throw new AppException(502, "chat_provider_error", "Provider unavailable.");
        if (BeforeReply is not null) await BeforeReply(ct);
        return "لنحل المسألة خطوة بخطوة.";
    }
}
