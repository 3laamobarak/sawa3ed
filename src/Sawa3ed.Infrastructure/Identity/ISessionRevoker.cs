namespace Sawa3ed.Infrastructure.Identity;

public interface ISessionRevoker
{
    // A null sessionId revokes all of the user's sessions.
    Task RevokeAsync(string userId, Guid? sessionId, CancellationToken ct);
}
