using System.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Configuration;
using Sawa3ed.Infrastructure.Email;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class AuthService(AppDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, TokenService tokens, EmailQueue email,
    IOptions<JwtOptions> options, TimeProvider clock) : IAuthService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw AppException.Invalid("A display name is required.");
        var address = request.Email.Trim();
        if (await users.FindByEmailAsync(address) is not null) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var user = new ApplicationUser { Email = address, UserName = address, DisplayName = request.DisplayName.Trim(), CreatedAtUtc = Now };
        Ensure(await users.CreateAsync(user, request.Password));
        Ensure(await users.AddToRoleAsync(user, Roles.Student));
        await IssueOtpAsync(user, OtpPurpose.ConfirmEmail, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            // Do comparable password-hashing work for an unknown account.
            _ = users.PasswordHasher.HashPassword(new ApplicationUser(), request.Password);
            throw AppException.Unauthorized();
        }
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded || !user.EmailConfirmed || user.IsDeleted) throw AppException.Unauthorized();
        var session = new AuthSession { UserId = user.Id, SecurityStamp = user.SecurityStamp!, ExpiresAtUtc = Now.AddDays(options.Value.RefreshDays) };
        var raw = TokenService.NewRefreshToken();
        db.Sessions.Add(session);
        db.RefreshTokens.Add(new() { Session = session, Hash = TokenService.Hash(raw), CreatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
        return tokens.Issue(user, session, await users.GetRolesAsync(user), raw);
    }

    public async Task<TokenResponse> RefreshAsync(string token, CancellationToken ct)
    {
        var hash = TokenService.Hash(token);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var stored = await db.RefreshTokens.Include(x => x.Session).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Hash == hash, ct);
        if (stored is null) throw AppException.Unauthorized();
        var session = stored.Session;
        if (stored.UsedAtUtc is not null)
        {
            // A replay invalidates the entire session, including the newly rotated token.
            session.RevokedAtUtc = Now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            throw AppException.Unauthorized();
        }
        if (session.RevokedAtUtc is not null || session.ExpiresAtUtc <= Now ||
            session.User.IsDeleted || !session.User.EmailConfirmed ||
            session.SecurityStamp != session.User.SecurityStamp || await users.IsLockedOutAsync(session.User))
            throw AppException.Unauthorized();
        stored.UsedAtUtc = Now;
        var raw = TokenService.NewRefreshToken();
        db.RefreshTokens.Add(new() { SessionId = session.Id, Hash = TokenService.Hash(raw), CreatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
        var response = tokens.Issue(session.User, session, await users.GetRolesAsync(session.User), raw);
        await tx.CommitAsync(ct);
        return response;
    }

    public async Task RequestOtpAsync(OtpRequest request, CancellationToken ct)
    {
        if (!Enum.IsDefined(request.Purpose)) throw AppException.Invalid("Invalid OTP purpose.");
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || (request.Purpose == OtpPurpose.ConfirmEmail && user.EmailConfirmed)) return;
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await IssueOtpAsync(user, request.Purpose, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task IssueOtpAsync(ApplicationUser user, OtpPurpose purpose, CancellationToken ct)
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
        challenge.Hash = tokens.HashOtp(challenge.Id, code);
        // Resends inside a validity window do not reset the guess budget.
        if (challenge.ExpiresAtUtc <= Now) challenge.Attempts = 0;
        challenge.CreatedAtUtc = Now;
        challenge.ExpiresAtUtc = Now.AddMinutes(10);
        challenge.ConsumedAtUtc = null;
        email.Enqueue(user.Email!, purpose == OtpPurpose.ConfirmEmail ? "Sawa3ed: confirm your email" : "Sawa3ed: reset your password",
            $"Your {(purpose == OtpPurpose.ConfirmEmail ? "email confirmation" : "password reset")} code is {code}. It expires in 10 minutes. If you did not request it, ignore this message.");
    }

    private async Task<OtpChallenge?> MatchOtpAsync(ApplicationUser user, OtpPurpose purpose, string code, CancellationToken ct)
    {
        var challenge = await db.OtpChallenges.SingleOrDefaultAsync(x => x.UserId == user.Id && x.Purpose == purpose, ct);
        if (challenge is null || challenge.ExpiresAtUtc <= Now || challenge.ConsumedAtUtc is not null || challenge.Attempts >= 5) return null;
        challenge.Attempts++;
        var matches = tokens.VerifyOtp(challenge.Id, code, challenge.Hash);
        if (matches) challenge.ConsumedAtUtc = Now;
        return matches ? challenge : null;
    }

    public async Task ConfirmEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var challenge = await MatchOtpAsync(user, OtpPurpose.ConfirmEmail, request.Code, ct);
        if (challenge is not null)
        {
            user.EmailConfirmed = true;
            Ensure(await users.UpdateAsync(user));
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct); // Persist failed guesses too.
        if (challenge is null) throw AppException.Unauthorized();
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim()) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var challenge = await MatchOtpAsync(user, OtpPurpose.ResetPassword, request.Code, ct);
        if (challenge is not null)
        {
            var identityToken = await users.GeneratePasswordResetTokenAsync(user);
            Ensure(await users.ResetPasswordAsync(user, identityToken, request.NewPassword));
            await RevokeAllAsync(user.Id, ct);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        if (challenge is null) throw AppException.Unauthorized();
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId) ?? throw AppException.Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        Ensure(await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword));
        await RevokeAllAsync(userId, ct);
        await tx.CommitAsync(ct);
    }

    public async Task<UserResponse> MeAsync(string userId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId) ?? throw AppException.Unauthorized();
        return new(user.Id, user.Email!, user.DisplayName, user.EmailConfirmed, (await users.GetRolesAsync(user)).ToArray());
    }

    public async Task LogoutAsync(string userId, Guid sessionId, bool allSessions, CancellationToken ct)
    {
        if (allSessions) await RevokeAllAsync(userId, ct);
        else await db.Sessions.Where(x => x.Id == sessionId && x.UserId == userId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);
    }
    private Task<int> RevokeAllAsync(string userId, CancellationToken ct) => db.Sessions
        .Where(x => x.UserId == userId && x.RevokedAtUtc == null)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, Now), ct);

    internal static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw AppException.Invalid(string.Join(" ", result.Errors.Select(e => e.Description)));
    }
}
