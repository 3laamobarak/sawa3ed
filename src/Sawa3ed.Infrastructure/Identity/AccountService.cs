using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class AccountService(AppDbContext db, UserManager<ApplicationUser> users,
    IOtpChallengeService challenges, TimeProvider clock) : IAccountService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw AppException.Invalid("A display name is required.");
        var address = request.Email.Trim();
        if (await users.FindByEmailAsync(address) is not null) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = new ApplicationUser { Email = address, UserName = address, DisplayName = request.DisplayName.Trim(), CreatedAtUtc = Now };
        IdentityResultGuard.Ensure(await users.CreateAsync(user, request.Password));
        IdentityResultGuard.Ensure(await users.AddToRoleAsync(user, Roles.Student));
        await challenges.IssueAsync(user, OtpPurpose.ConfirmEmail, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<UserResponse> MeAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId) ?? throw AppException.Unauthorized();
        return new(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, (await users.GetRolesAsync(user)).ToArray());
    }
}
