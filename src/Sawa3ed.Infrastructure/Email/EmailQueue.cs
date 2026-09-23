using Microsoft.AspNetCore.DataProtection;
using Sawa3ed.Application.Email;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Email;

public sealed class EmailQueue(AppDbContext db, IDataProtectionProvider protection, TimeProvider clock) : IEmailQueue
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
