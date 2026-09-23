using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class PasswordService(AppDbContext db, UserManager<ApplicationUser> users,
    IOtpChallengeService challenges, ISessionRevoker sessions) : IPasswordService
{
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var valid = await challenges.TryConsumeAsync(user, OtpPurpose.ResetPassword, request.Code, ct);
        if (valid)
        {
            var identityToken = await users.GeneratePasswordResetTokenAsync(user);
            IdentityResultGuard.Ensure(await users.ResetPasswordAsync(user, identityToken, request.NewPassword));
            await sessions.RevokeAsync(user.Id, null, ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (!valid) throw AppException.Unauthorized();
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        IdentityResultGuard.Ensure(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        await sessions.RevokeAsync(userId, null, ct);
        await tx.CommitAsync(ct);
    }
}
