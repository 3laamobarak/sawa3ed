using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sawa3ed.Application.Auth;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class AdminBootstrapper(AppDbContext db, RoleManager<IdentityRole> roles,
    UserManager<ApplicationUser> users, IConfiguration configuration, TimeProvider clock)
{
    public async Task SeedAdminAsync(CancellationToken ct)
    {
        var email = configuration["Bootstrap:Email"];
        var password = configuration["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Bootstrap:Email and Bootstrap:Password through user-secrets or the environment.");
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Bootstrap refuses to promote an existing account. Use an authorized Admin account.");
        if (!await roles.RoleExistsAsync(Roles.Admin)) throw new InvalidOperationException("Run --migrate first.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser { UserName = email, Email = email, DisplayName = "Administrator", EmailConfirmed = true, CreatedAtUtc = clock.GetUtcNow().UtcDateTime };
        IdentityResultGuard.Ensure(await users.CreateAsync(user, password));
        IdentityResultGuard.Ensure(await users.AddToRoleAsync(user, Roles.Admin));
        await tx.CommitAsync(ct);
    }
}
