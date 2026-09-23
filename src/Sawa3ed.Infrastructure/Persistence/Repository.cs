using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Common;
using Sawa3ed.Domain.Common;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class Repository<T>(AppDbContext db) : IRepository<T> where T : Entity
{
    // FindAsync may return a tracked soft-deleted entity. Always use the filtered query.
    public Task<T?> GetAsync(Guid id, bool tracking = false, CancellationToken ct = default) =>
        (tracking ? db.Set<T>() : db.Set<T>().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

    public async Task<Page<TResult>> PageAsync<TResult>(Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> projection, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw AppException.Invalid("Page must be 1–100000; pageSize must be 1–100.");
        var query = db.Set<T>().AsNoTracking().Where(predicate);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(projection).ToListAsync(ct);
        return new(items, page, pageSize, count);
    }
    public void Add(T entity) => db.Set<T>().Add(entity);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
}

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
