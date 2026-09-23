namespace Sawa3ed.Application.Files;

public sealed record FileResponse(Guid Id, string Name, string Folder, string ContentType, long Length, DateTime CreatedAtUtc);
