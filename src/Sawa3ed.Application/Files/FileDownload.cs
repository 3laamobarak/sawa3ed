namespace Sawa3ed.Application.Files;

public sealed record FileDownload(Stream Content, string Name, string ContentType, long Length);
