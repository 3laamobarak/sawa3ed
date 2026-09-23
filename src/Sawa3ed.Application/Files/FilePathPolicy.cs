using Sawa3ed.Application.Common;

namespace Sawa3ed.Application.Files;

public static class FilePathPolicy
{
    public static string SafePath(string path, bool allowFolders)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > (allowFolders ? 500 : 255) ||
            path.Any(c => char.IsControl(c) || "\\:*?\"<>|".Contains(c)) || path.StartsWith('/'))
            throw AppException.Invalid("Invalid file name or relative path.");
        var segments = path.Split('/');
        if ((!allowFolders && segments.Length != 1) || segments.Any(x => string.IsNullOrWhiteSpace(x) || x is "." or ".." || x.EndsWith('.') || x.EndsWith(' ')))
            throw AppException.Invalid("Unsafe relative path.");
        return path;
    }
}
