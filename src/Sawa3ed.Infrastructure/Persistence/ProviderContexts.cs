using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options, TimeProvider clock, ICurrentUser actor)
    : AppDbContext(options, clock, actor);
public sealed class SqlServerAppDbContext(DbContextOptions<SqlServerAppDbContext> options, TimeProvider clock, ICurrentUser actor)
    : AppDbContext(options, clock, actor);

internal sealed class DesignActor : ICurrentUser { public string? UserId => null; }
public sealed class SqliteDesignFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite("Data Source=sawa3ed.design.db").Options,
        TimeProvider.System, new DesignActor());
}
public sealed class SqlServerDesignFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqlServerAppDbContext>().UseSqlServer("Server=localhost;Database=Sawa3ed;Integrated Security=true;TrustServerCertificate=true").Options,
        TimeProvider.System, new DesignActor());
}
