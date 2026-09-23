namespace Sawa3ed.Application.Files;

public sealed record UploadFile(string Name, string? RelativePath, long Length, Stream Content);
