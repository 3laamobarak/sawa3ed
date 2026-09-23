using Microsoft.AspNetCore.Identity;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class RoleSeeder(RoleManager<IdentityRole> roles)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        foreach (var role in Roles.All)
        {
            ct.ThrowIfCancellationRequested();
            if (!await roles.RoleExistsAsync(role)) IdentityResultGuard.Ensure(await roles.CreateAsync(new IdentityRole(role)));
        }
    }
}
