using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class EmailVerificationService(AppDbContext db, UserManager<ApplicationUser> users,
    IOtpChallengeService challenges) : IEmailVerificationService
{
    public async Task RequestOtpAsync(OtpRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Purpose)) throw AppException.Invalid("Invalid OTP purpose.");
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || (request.Purpose == OtpPurpose.ConfirmEmail && user.EmailConfirmed)) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await challenges.IssueAsync(user, request.Purpose, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ConfirmEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var valid = await challenges.TryConsumeAsync(user, OtpPurpose.ConfirmEmail, request.Code, ct);
        if (valid)
        {
            user.EmailConfirmed = true;
            IdentityResultGuard.Ensure(await users.UpdateAsync(user));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct); // Persist failed guesses too.
        if (!valid) throw AppException.Unauthorized();
    }
}
