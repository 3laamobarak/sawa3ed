namespace Sawa3ed.Infrastructure.Identity;

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
