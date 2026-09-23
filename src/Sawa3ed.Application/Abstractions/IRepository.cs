using System.Linq.Expressions;
using Sawa3ed.Domain.Common;

namespace Sawa3ed.Application.Abstractions;

public interface IRepository<T> where T : Entity
{
    Task<T?> GetAsync(Guid id, bool tracking = false, CancellationToken ct = default);
    Task<Page<TResult>> PageAsync<TResult>(Expression<Func<T, bool>> predicate,
        Expression<Func<T, TResult>> projection, int page = 1, int pageSize = 20, CancellationToken ct = default);
    void Add(T entity);
    void Remove(T entity);
}
