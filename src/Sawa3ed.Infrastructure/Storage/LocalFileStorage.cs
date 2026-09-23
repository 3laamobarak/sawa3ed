using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Common;
using Sawa3ed.Application.Files;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Storage;

public sealed class LocalFileStorage(IOptions<StorageOptions> options, IUploadScanner scanner) : IFileStorage
{
    public async Task<StoredObject> WriteAsync(UploadFile upload, CancellationToken ct)
    {
        if (upload.Length <= 0 || upload.Length > options.Value.MaxFileBytes) throw AppException.Invalid("File size exceeds the configured limit or is empty.");
        var extension = Path.GetExtension(upload.Name).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".pdf" or ".mp4"))
            throw AppException.Invalid("Supported files: JPEG, PNG, WebP, PDF and MP4.");
        var key = Guid.NewGuid().ToString("N") + extension;
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            long length = 0;
            var header = new byte[16];
            var headerLength = 0;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await upload.Content.ReadAsync(buffer, ct)) > 0)
                {
                    length += read;
                    if (length > options.Value.MaxFileBytes || length > upload.Length) throw AppException.Invalid("File exceeds declared size.");
                    var copy = Math.Min(header.Length - headerLength, read);
                    buffer.AsSpan(0, copy).CopyTo(header.AsSpan(headerLength));
                    headerLength += copy;
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            if (length != upload.Length) throw AppException.Invalid("Incomplete file upload.");
            var contentType = FileSignaturePolicy.DetectContentType(extension, header.AsSpan(0, headerLength));
            await scanner.ScanAsync(path, ct);
            return new(key, contentType, length, Convert.ToHexString(hash.GetHashAndReset()));
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }
    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var path = Resolve(key);
        if (!File.Exists(path)) throw AppException.NotFound();
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous));
    }
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(Resolve(key));
        return Task.CompletedTask;
    }
    private string Resolve(string key)
    {
        if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _) || key != Path.GetFileName(key) || key.Contains('\\'))
            throw AppException.Invalid("Invalid storage key.");
        return Path.Combine(Path.GetFullPath(options.Value.RootPath), key);
    }

}
