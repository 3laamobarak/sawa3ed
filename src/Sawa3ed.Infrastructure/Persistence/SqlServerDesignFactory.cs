using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sawa3ed.Infrastructure.Persistence;

public sealed class SqlServerDesignFactory : IDesignTimeDbContextFactory<SqlServerAppDbContext>
{
    public SqlServerAppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<SqlServerAppDbContext>().UseSqlServer("Server=localhost;Database=Sawa3ed;Integrated Security=true;TrustServerCertificate=true").Options,
        TimeProvider.System, new DesignActor());
}
