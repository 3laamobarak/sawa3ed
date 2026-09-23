using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Chat;
using Sawa3ed.Application.Files;
using Sawa3ed.Infrastructure.Chat;
using Sawa3ed.Infrastructure.Configuration;
using Sawa3ed.Infrastructure.Email;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Persistence;
using Sawa3ed.Infrastructure.Storage;

namespace Sawa3ed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt").ValidateDataAnnotations()
            .Validate(x => x.SigningKey != x.OtpPepper, "SigningKey and OtpPepper must be independent secrets.").ValidateOnStart();
        services.AddOptions<StorageOptions>().BindConfiguration("Storage").ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<EmailOptions>().BindConfiguration("Email")
            .Validate(x => environment.IsDevelopment() || environment.IsEnvironment("Testing") || !x.UseDevelopmentPickup, "Email pickup is restricted to Development/Testing.")
            .Validate(x => x.UseDevelopmentPickup || !string.IsNullOrWhiteSpace(x.Host), "Configure SMTP or development pickup.").ValidateOnStart();
        services.AddOptions<ChatOptions>().BindConfiguration("Chat").ValidateDataAnnotations()
            .Validate(x => !x.Enabled || (!string.IsNullOrWhiteSpace(x.ApiKey) && !string.IsNullOrWhiteSpace(x.Model) &&
                Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)),
                "Enabled chat requires a model, API key, and HTTPS base URL.").ValidateOnStart();
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
        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.User.RequireUniqueEmail = true;
            o.Password.RequiredLength = 12;
            o.Password.RequireDigit = true;
            o.Password.RequireNonAlphanumeric = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireLowercase = true;
            o.SignIn.RequireConfirmedEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddSignInManager().AddDefaultTokenProviders();
        services.Configure<PasswordHasherOptions>(o => o.IterationCount = 210000);
        services.AddDataProtection().SetApplicationName("Sawa3ed");
        services.AddOptions<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>().Configure<IConfiguration>((o, c) =>
            o.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(
                new DirectoryInfo(c["DataProtection:KeysPath"] ?? "App_Data/keys"), Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance));
        services.AddMemoryCache();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<TokenService>();
        services.AddScoped<EmailQueue>();
        services.AddScoped<DatabaseInitializer>();
        services.AddHostedService<EmailDispatcher>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<UploadScanner>();
        services.AddScoped<IChatService, ChatService>();
        services.AddHttpClient<IChatClient, CompatibleChatClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 20
            });
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<JwtOptions>>((o, settings) =>
        {
            var jwt = settings.Value;
            o.MapInboundClaims = false;
            o.RequireHttpsMetadata = true;
            o.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.FromSeconds(15),
                NameClaimType = "sub", RoleClaimType = "role"
            };
            o.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    var userId = principal.FindFirst("sub")?.Value;
                    var stamp = principal.FindFirst("sst")?.Value;
                    if (!Guid.TryParse(principal.FindFirst("sid")?.Value, out var sid)) { context.Fail("Invalid session."); return; }
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var clock = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
                    var now = clock.GetUtcNow().UtcDateTime;
                    var nowOffset = clock.GetUtcNow();
                    // One indexed projection. Deliberately not cached: logout/role changes take effect immediately.
                    var valid = await db.Sessions.AsNoTracking().AnyAsync(x => x.Id == sid && x.UserId == userId &&
                        x.RevokedAtUtc == null && x.ExpiresAtUtc > now && x.SecurityStamp == stamp &&
                        x.User.SecurityStamp == stamp && x.User.EmailConfirmed &&
                        (x.User.LockoutEnd == null || x.User.LockoutEnd <= nowOffset), context.HttpContext.RequestAborted);
                    if (!valid) context.Fail("Session expired or revoked.");
                }
            };
        });
        return services;
    }
}
