using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Chat;
using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Infrastructure.Persistence;

internal static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        services.AddDbContext<SqlServerAppDbContext>((sp, o) => o.UseSqlServer(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Default"), sql => sql.CommandTimeout(30)));
        services.AddDbContext<SqliteAppDbContext>((sp, o) => o.UseSqlite(
            sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));
        services.AddScoped<AppDbContext>(sp => (sp.GetRequiredService<IConfiguration>()["Database:Provider"] ?? "Sqlite").ToLowerInvariant() switch
        {
            "sqlserver" => sp.GetRequiredService<SqlServerAppDbContext>(),
            "sqlite" => sp.GetRequiredService<SqliteAppDbContext>(),
            _ => throw new InvalidOperationException("Database:Provider must be Sqlite or SqlServer.")
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IApplicationReadiness, DatabaseReadiness>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
