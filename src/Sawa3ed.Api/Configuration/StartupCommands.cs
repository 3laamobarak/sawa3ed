using Sawa3ed.Infrastructure.Identity;

namespace Sawa3ed.Api.Configuration;

internal static class StartupCommands
{
    public static async Task<bool> RunStartupCommandsAsync(this WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        if (args.Contains("--migrate"))
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().MigrateAsync(CancellationToken.None);
            return true;
        }
        if (args.Contains("--seed-admin"))
        {
            await scope.ServiceProvider.GetRequiredService<AdminBootstrapper>().SeedAdminAsync(CancellationToken.None);
            return true;
        }
        if (app.Environment.IsDevelopment())
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().MigrateAsync(CancellationToken.None);
        return false;
    }
}
