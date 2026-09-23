using Sawa3ed.Application.Auth;

namespace Sawa3ed.Infrastructure.Identity;

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
