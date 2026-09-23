using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class SqliteDesignFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite("Data Source=sawa3ed.design.db").Options,
        TimeProvider.System, new DesignActor());
}
