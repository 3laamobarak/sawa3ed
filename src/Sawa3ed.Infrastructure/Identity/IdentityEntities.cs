using Microsoft.AspNetCore.Identity;
using Sawa3ed.Application.Auth;

namespace Sawa3ed.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AuthSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string SecurityStamp { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public AuthSession Session { get; set; } = null!;
    public string Hash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}

public sealed class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public OtpPurpose Purpose { get; set; }
    public string Hash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public int Attempts { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
