namespace Sawa3ed.Infrastructure.Email;

public sealed class EmailOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string To { get; set; } = "";
    public string Subject { get; set; } = "";
    public string ProtectedBody { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime NextAttemptAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public int Attempts { get; set; }
}
