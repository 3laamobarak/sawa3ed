using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Chat;
using Sawa3ed.Infrastructure.Email;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string Password = "A-strong-test-password-123!";
    private readonly string root = Path.Combine(Path.GetTempPath(), "sawa3ed-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string? sqlConnection = Environment.GetEnvironmentVariable("SAWA3ED_TEST_SQLSERVER");
    private bool started;
    public FakeChat Chat { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(root);
        var connection = string.IsNullOrWhiteSpace(sqlConnection) ? $"Data Source={Path.Combine(root, "test.db")}"
            : new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlConnection) { InitialCatalog = "Sawa3edTest_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = connection,
            ["Database:Provider"] = string.IsNullOrWhiteSpace(sqlConnection) ? "Sqlite" : "SqlServer",
            ["Jwt:SigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)),
            ["Jwt:OtpPepper"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(64)),
            ["Email:UseDevelopmentPickup"] = "true",
            ["DataProtection:KeysPath"] = Path.Combine(root, "keys"),
            ["Storage:RootPath"] = Path.Combine(root, "uploads"),
            ["Logging:LogLevel:Default"] = "Error"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(Chat);
            var dispatcher = services.SingleOrDefault(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(EmailDispatcher));
            if (dispatcher is not null) services.Remove(dispatcher);
        });
    }
    public async Task<HttpClient> StartAsync()
    {
        var client = CreateClient(new() { AllowAutoRedirect = false });
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().MigrateAsync(default);
        started = true;
        return client;
    }
    public async Task<string> LatestCodeAsync(string email, OtpPurpose purpose)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var subject = purpose == OtpPurpose.ConfirmEmail ? "Sawa3ed: confirm your email" : "Sawa3ed: reset your password";
        var row = await db.Set<EmailOutbox>().Where(x => x.To == email && x.Subject == subject).OrderByDescending(x => x.ExpiresAtUtc).FirstAsync();
        var plaintext = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("Sawa3ed.EmailOutbox.v1").Unprotect(row.ProtectedBody);
        return Regex.Match(plaintext, @"\b\d{6}\b").Value;
    }
    public async Task<(string Id, TokenResponse Tokens)> AccountAsync(HttpClient client, string role = Roles.Student)
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        // Helper creates independent confirmed fixtures without consuming the public auth limiter.
        await using var scope = Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { Email = email, UserName = email, DisplayName = "Test User", EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        var tokens = await scope.ServiceProvider.GetRequiredService<IAuthSessionService>().LoginAsync(new(email, Password), default);
        return (user.Id, tokens);
    }
    public static void Authenticate(HttpClient client, TokenResponse tokens) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    protected override void Dispose(bool disposing)
    {
        if (disposing && started && !string.IsNullOrWhiteSpace(sqlConnection))
        {
            // WebApplicationFactory's sync disposal re-enters this override via DisposeAsync.
            // Claim cleanup before touching the service provider so it runs exactly once.
            started = false;
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
        }
        base.Dispose(disposing);
        if (disposing && Directory.Exists(root))
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
