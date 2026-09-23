using Sawa3ed.Application.Auth;

namespace Sawa3ed.Infrastructure.Identity;

public interface IOtpChallengeService
{
    // Both operations stage changes. The caller owns the serializable transaction
    // and must persist failed guesses as well as successful consumption.
    Task IssueAsync(ApplicationUser user, OtpPurpose purpose, CancellationToken ct);
    Task<bool> TryConsumeAsync(ApplicationUser user, OtpPurpose purpose, string code, CancellationToken ct);
}
