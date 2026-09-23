namespace Sawa3ed.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
