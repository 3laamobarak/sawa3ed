using Sawa3ed.Infrastructure.Email;

namespace Sawa3ed.Tests;

public sealed class FakeEmailTransport : IEmailTransport
{
    public bool Fail { get; set; }
    public List<OutboundEmail> Delivered { get; } = [];

    public Task SendAsync(OutboundEmail email, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (Fail) throw new IOException("Simulated transport failure.");
        Delivered.Add(email);
        return Task.CompletedTask;
    }
}
