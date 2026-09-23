using Microsoft.AspNetCore.DataProtection;
using Sawa3ed.Infrastructure.Persistence;

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
public sealed class EmailQueue(AppDbContext db, IDataProtectionProvider protection, TimeProvider clock)
{
    public void Enqueue(string email, string subject, string body)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        db.Set<EmailOutbox>().Add(new()
        {
            To = email, Subject = subject,
            ProtectedBody = protection.CreateProtector("Sawa3ed.EmailOutbox.v1").Protect(body),
            ExpiresAtUtc = now.AddMinutes(10), NextAttemptAtUtc = now
        });
    }
}
