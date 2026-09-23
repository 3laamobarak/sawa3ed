using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure.Configuration;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

internal static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().BindConfiguration("Jwt").ValidateDataAnnotations()
            .Validate(x => x.SigningKey != x.OtpPepper, "SigningKey and OtpPepper must be independent secrets.").ValidateOnStart();
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
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IEmailVerificationService, EmailVerificationService>();
        services.AddScoped<IOtpChallengeService, OtpChallengeService>();
        services.AddSingleton<IOtpHasher, HmacOtpHasher>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<ISessionRevoker, SessionRevoker>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<RoleSeeder>();
        services.AddScoped<AdminBootstrapper>();
        services.AddScoped<SessionValidationEvents>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        return services;
    }
}
