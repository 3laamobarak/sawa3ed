using Sawa3ed.Domain.Common;

namespace Sawa3ed.Domain.Files;

public sealed class StoredFile : Entity
{
    public string OwnerId { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string OriginalName { get; set; } = "";
    public string Folder { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
}
