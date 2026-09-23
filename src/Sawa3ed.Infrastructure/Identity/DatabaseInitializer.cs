using Microsoft.EntityFrameworkCore;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class DatabaseInitializer(AppDbContext db, RoleSeeder roles)
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        await db.Database.MigrateAsync(ct);
        await roles.SeedAsync(ct);
    }
}
