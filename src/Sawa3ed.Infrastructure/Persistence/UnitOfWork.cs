using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Domain.Common;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    private readonly Dictionary<Type, object> repositories = [];
    public IRepository<T> Repository<T>() where T : Entity
    {
        if (!repositories.TryGetValue(typeof(T), out var repository))
            repositories[typeof(T)] = repository = new Repository<T>(db);
        return (IRepository<T>)repository;
    }
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) throw new InvalidOperationException("Nested transactions are not supported.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            await action(ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }
}
