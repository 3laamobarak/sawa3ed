using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Sawa3ed.Infrastructure.Configuration;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Email;

public sealed class EmailDispatcher(IServiceScopeFactory scopes, IDataProtectionProvider protection,
    IOptions<EmailOptions> options, TimeProvider clock, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await DispatchAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError("Email dispatch failed ({ErrorType})", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task DispatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
                using var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(options.Value.From));
                message.To.Add(MailboxAddress.Parse(item.To));
                message.Subject = item.Subject;
                message.Body = new TextPart("plain") { Text = body };
                if (options.Value.UseDevelopmentPickup)
                {
                    Directory.CreateDirectory(options.Value.PickupPath);
                    await message.WriteToAsync(Path.Combine(options.Value.PickupPath, item.Id.ToString("N") + ".eml"), ct);
                }
                else
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    using var smtp = new SmtpClient();
                    await smtp.ConnectAsync(options.Value.Host, options.Value.Port,
                        options.Value.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
                    if (!string.IsNullOrWhiteSpace(options.Value.UserName))
                        await smtp.AuthenticateAsync(options.Value.UserName, options.Value.Password, timeout.Token);
                    await smtp.SendAsync(message, timeout.Token);
                    await smtp.DisconnectAsync(true, timeout.Token);
                }
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
