using System.Linq.Expressions;
using Sawa3ed.Domain.Common;

namespace Sawa3ed.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
}

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public interface IRepository<T> where T : Entity
{
    Task<T?> GetAsync(Guid id, bool tracking = false, CancellationToken ct = default);
    Task<Page<TResult>> PageAsync<TResult>(Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> projection, int page = 1, int pageSize = 20, CancellationToken ct = default);
    void Add(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : Entity;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken ct = default);
}
