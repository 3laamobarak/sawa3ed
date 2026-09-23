using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sawa3ed.Application.Email;
using Sawa3ed.Infrastructure.Email;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Tests;

public sealed class EmailOutboxTests
{
    [Fact]
    public async Task Successful_delivery_uses_transport_and_clears_protected_payload()
    {
        using var factory = new ApiFactory();
        using var client = await factory.StartAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<IEmailQueue>().Enqueue("student@example.test", "Confirmation", "Your code is 123456.");
        await db.SaveChangesAsync();
        var before = await db.Set<EmailOutbox>().AsNoTracking().SingleAsync();
        Assert.DoesNotContain("123456", before.ProtectedBody);
        var transport = new FakeEmailTransport();
        var processor = CreateProcessor(scope.ServiceProvider, db, transport);

        await processor.ProcessAsync(default);
        await processor.ProcessAsync(default);

        Assert.Equal("Your code is 123456.", Assert.Single(transport.Delivered).Body);
        var delivered = await db.Set<EmailOutbox>().AsNoTracking().SingleAsync();
        Assert.NotNull(delivered.SentAtUtc);
        Assert.Equal("", delivered.ProtectedBody);
        Assert.Equal(1, delivered.Attempts);
    }

    [Fact]
    public async Task Failed_delivery_keeps_payload_and_retries_only_after_lease_expires()
    {
        using var factory = new ApiFactory();
        using var client = await factory.StartAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        scope.ServiceProvider.GetRequiredService<IEmailQueue>().Enqueue("student@example.test", "Confirmation", "Your code is 123456.");
        await db.SaveChangesAsync();
        var transport = new FakeEmailTransport { Fail = true };
        var processor = CreateProcessor(scope.ServiceProvider, db, transport);

        await processor.ProcessAsync(default);
        var failed = await db.Set<EmailOutbox>().AsNoTracking().SingleAsync();
        Assert.Null(failed.SentAtUtc);
        Assert.NotEmpty(failed.ProtectedBody);
        Assert.Equal(1, failed.Attempts);
        transport.Fail = false;
        await processor.ProcessAsync(default);
        Assert.Empty(transport.Delivered);
        await db.Set<EmailOutbox>().ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttemptAtUtc, DateTime.UtcNow.AddSeconds(-1)));
        await processor.ProcessAsync(default);
        Assert.Single(transport.Delivered);
        Assert.Equal(2, (await db.Set<EmailOutbox>().AsNoTracking().SingleAsync()).Attempts);
    }

    private static EmailOutboxProcessor CreateProcessor(IServiceProvider services, AppDbContext db, IEmailTransport transport) =>
        new(db, services.GetRequiredService<IDataProtectionProvider>(), transport, TimeProvider.System, NullLogger<EmailOutboxProcessor>.Instance);
}
