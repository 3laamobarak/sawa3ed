using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Email;

public sealed class EmailOutboxProcessor(AppDbContext db, IDataProtectionProvider protection,
    IEmailTransport transport, TimeProvider clock, ILogger<EmailOutboxProcessor> logger)
{
    public async Task ProcessAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var pending = await db.Set<EmailOutbox>().AsNoTracking()
            .Where(x => x.SentAtUtc == null && x.NextAttemptAtUtc <= now && x.ExpiresAtUtc > now && x.Attempts < 5)
            .OrderBy(x => x.NextAttemptAtUtc).Take(20).ToListAsync(ct);
        foreach (var item in pending)
        {
            // Atomic lease prevents two replicas dispatching the same row concurrently.
            var claimed = await db.Set<EmailOutbox>().Where(x => x.Id == item.Id && x.SentAtUtc == null && x.NextAttemptAtUtc <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, now.AddMinutes(1))
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
            if (claimed == 0) continue;
            try
            {
                var body = protection.CreateProtector("Sawa3ed.EmailOutbox.v1").Unprotect(item.ProtectedBody);
                await transport.SendAsync(new OutboundEmail(item.Id, item.To, item.Subject, body), ct);
                await db.Set<EmailOutbox>().Where(x => x.Id == item.Id).ExecuteUpdateAsync(s =>
                    s.SetProperty(x => x.SentAtUtc, clock.GetUtcNow().UtcDateTime).SetProperty(x => x.ProtectedBody, ""), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning("Email {MessageId} delivery failed ({ErrorType}); retry is scheduled", item.Id, ex.GetType().Name);
            }
        }
        await db.Set<EmailOutbox>().Where(x => x.ExpiresAtUtc < now).ExecuteDeleteAsync(ct);
    }
}
