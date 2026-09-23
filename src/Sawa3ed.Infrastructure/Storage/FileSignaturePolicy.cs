using Sawa3ed.Application.Common;

namespace Sawa3ed.Infrastructure.Storage;

public static class FileSignaturePolicy
{
    public static string DetectContentType(string extension, ReadOnlySpan<byte> header) => extension switch
    {
        ".jpg" or ".jpeg" when header.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) => "image/jpeg",
        ".png" when header.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => "image/png",
        ".webp" when header.Length >= 12 && header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8) => "image/webp",
        ".pdf" when header.StartsWith("%PDF-"u8) => "application/pdf",
        ".mp4" when header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8) => "video/mp4",
        _ => throw AppException.Invalid("File signature does not match its extension.")
    };
}
