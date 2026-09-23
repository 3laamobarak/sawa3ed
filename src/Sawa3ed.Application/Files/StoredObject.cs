namespace Sawa3ed.Application.Files;

public sealed record StoredObject(string Key, string ContentType, long Length, string Sha256);
