namespace Sawa3ed.Infrastructure.Identity;

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
