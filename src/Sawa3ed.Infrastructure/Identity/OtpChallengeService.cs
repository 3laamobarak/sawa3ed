using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Email;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class OtpChallengeService(AppDbContext db, IOtpHasher hasher, IEmailQueue email, TimeProvider clock) : IOtpChallengeService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task IssueAsync(ApplicationUser user, OtpPurpose purpose, CancellationToken ct)
    {
        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Purpose == purpose, ct);
        if (challenge is not null && challenge.CreatedAtUtc.AddMinutes(1) > Now) return;
        // Exhausting guesses also blocks issuing a fresh challenge until expiry.
        if (challenge is not null && challenge.Attempts >= 5 && challenge.ExpiresAtUtc > Now) return;
        if (challenge is null)
        {
            challenge = new() { UserId = user.Id, Purpose = purpose };
            db.OtpChallenges.Add(challenge);
        }
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        challenge.Hash = hasher.Hash(challenge.Id, code);
        // Resends inside a validity window do not reset the guess budget.
        if (challenge.ExpiresAtUtc <= Now) challenge.Attempts = 0;
        challenge.CreatedAtUtc = Now;
        challenge.ExpiresAtUtc = Now.AddMinutes(10);
        challenge.ConsumedAtUtc = null;
        email.Enqueue(user.Email!, purpose == OtpPurpose.ConfirmEmail ? "Sawa3ed: confirm your email" : "Sawa3ed: reset your password",
            $"Your {(purpose == OtpPurpose.ConfirmEmail ? "email confirmation" : "password reset")} code is {code}. It expires in 10 minutes. If you did not request it, ignore this message.");
    }

    public async Task<bool> TryConsumeAsync(ApplicationUser user, OtpPurpose purpose, string code, CancellationToken ct)
    {
        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Purpose == purpose, ct);
        if (challenge is null || challenge.ExpiresAtUtc <= Now || challenge.ConsumedAtUtc is not null || challenge.Attempts >= 5) return false;
        challenge.Attempts++;
        var matches = hasher.Verify(challenge.Id, code, challenge.Hash);
        if (matches) challenge.ConsumedAtUtc = Now;
        return matches;
    }
}
