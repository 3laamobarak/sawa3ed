using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class DatabaseReadiness(AppDbContext db) : IApplicationReadiness
{
    public Task<bool> IsReadyAsync(CancellationToken ct) => db.Roles.AsNoTracking().AnyAsync(ct);
}
