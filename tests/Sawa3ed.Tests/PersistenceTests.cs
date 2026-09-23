using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Domain.Files;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Tests;

public sealed class PersistenceTests
{
    private sealed class Actor : ICurrentUser { public string? UserId => "test-actor"; }
    private static AppDbContext Create(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, TimeProvider.System, new Actor());

    [Fact]
    public async Task All_save_overloads_stamp_audit_and_convert_delete_to_soft_delete()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Create(connection);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
        db.SaveChanges();
        var file = new StoredFile { OwnerId = "owner", StorageKey = "key", OriginalName = "one.pdf" };
        db.Files.Add(file);
        await db.SaveChangesAsync(true, default);
        var created = file.CreatedAtUtc;
        Assert.Equal("test-actor", file.CreatedBy);
        file.OriginalName = "two.pdf";
        db.SaveChanges(true);
        Assert.Equal(created, file.CreatedAtUtc);
        Assert.NotNull(file.UpdatedAtUtc);
        db.Files.Remove(file);
        await db.SaveChangesAsync();
        Assert.Empty(await db.Files.ToListAsync());
        Assert.Null(await new Repository<StoredFile>(db).GetAsync(file.Id));
        var tombstone = await db.Files.IgnoreQueryFilters().SingleAsync();
        Assert.True(tombstone.IsDeleted);
        Assert.Equal("test-actor", tombstone.DeletedBy);
    }

    [Fact]
    public async Task Repository_does_not_commit_and_transaction_rolls_back_all_changes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = Create(connection);
        await db.Database.EnsureCreatedAsync();
        db.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
        await db.SaveChangesAsync();
        var unit = new UnitOfWork(db);
        unit.Repository<StoredFile>().Add(new() { OwnerId = "owner", StorageKey = "one" });
        Assert.Equal(0, await db.Files.CountAsync());
        await unit.SaveChangesAsync();
        Assert.Equal(1, await db.Files.CountAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInTransactionAsync(async ct =>
        {
            unit.Repository<StoredFile>().Add(new() { OwnerId = "owner", StorageKey = "two" });
            await unit.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated failure");
        }));
        Assert.Equal(1, await db.Files.CountAsync());
    }

    [Fact]
    public async Task Concurrency_token_detects_lost_updates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var first = Create(connection);
        await first.Database.EnsureCreatedAsync();
        first.Users.Add(new ApplicationUser { Id = "owner", UserName = "owner" });
        first.Files.Add(new() { OwnerId = "owner", StorageKey = "one" });
        await first.SaveChangesAsync();
        await using var second = Create(connection);
        var stale = await second.Files.SingleAsync();
        (await first.Files.SingleAsync()).OriginalName = "new.pdf";
        await first.SaveChangesAsync();
        stale.OriginalName = "stale.pdf";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public void Domain_and_application_do_not_reference_infrastructure_or_web_frameworks()
    {
        foreach (var assembly in new[] { typeof(StoredFile).Assembly, typeof(IUnitOfWork).Assembly })
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), x => x.Name!.Contains("EntityFrameworkCore") || x.Name.Contains("AspNetCore") || x.Name.Contains("Infrastructure"));
    }
}
