using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Auth;
using Sawa3ed.Application.Common;
using Sawa3ed.Infrastructure.Configuration;
using Sawa3ed.Infrastructure.Persistence;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class AuthSessionService(AppDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IAccessTokenIssuer tokens, ISessionRevoker sessions,
    IOptions<JwtOptions> options, TimeProvider clock) : IAuthSessionService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

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
        var raw = RefreshTokenGenerator.NewRefreshToken();
        db.Sessions.Add(session);
        db.RefreshTokens.Add(new() { Session = session, Hash = RefreshTokenGenerator.Hash(raw), CreatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
        return tokens.Issue(user, session, await users.GetRolesAsync(user), raw);
    }

    public async Task<TokenResponse> RefreshAsync(string token, CancellationToken ct)
    {
        var hash = RefreshTokenGenerator.Hash(token);
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
        var raw = RefreshTokenGenerator.NewRefreshToken();
        db.RefreshTokens.Add(new() { SessionId = session.Id, Hash = RefreshTokenGenerator.Hash(raw), CreatedAtUtc = Now });
        await db.SaveChangesAsync(ct);
        var response = tokens.Issue(session.User, session, await users.GetRolesAsync(session.User), raw);
        await tx.CommitAsync(ct);
        return response;
    }

    public Task LogoutAsync(string userId, Guid sessionId, bool allSessions, CancellationToken ct) =>
        sessions.RevokeAsync(userId, allSessions ? null : sessionId, ct);
}
