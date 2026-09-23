using Sawa3ed.Application.Abstractions;
using Sawa3ed.Application.Common;
using Sawa3ed.Domain.Files;

namespace Sawa3ed.Application.Files;

public sealed class FileService(IUnitOfWork unitOfWork, IFileStorage storage) : IFileService
{
    public async Task<IReadOnlyList<FileResponse>> UploadAsync(string ownerId, IReadOnlyList<UploadFile> files, CancellationToken ct)
    {
        if (files.Count is < 1 or > 20) throw AppException.Invalid("Upload between 1 and 20 files.");
        var records = new List<StoredFile>();
        var objects = new List<string>();
        try
        {
            foreach (var file in files)
            {
                var name = SafePath(file.Name, allowFolders: false);
                var relative = SafePath(file.RelativePath ?? name, allowFolders: true);
                if (!string.Equals(relative.Split('/')[^1], name, StringComparison.Ordinal))
                    throw AppException.Invalid("Each relative path must end with its file name.");
                var folder = relative.Contains('/') ? relative[..relative.LastIndexOf('/')] : "";
                var stored = await storage.WriteAsync(file, ct);
                objects.Add(stored.Key);
                var record = new StoredFile
                {
                    OwnerId = ownerId, StorageKey = stored.Key, OriginalName = name, Folder = folder,
                    ContentType = stored.ContentType, Length = stored.Length, Sha256 = stored.Sha256
                };
                records.Add(record);
                unitOfWork.Repository<StoredFile>().Add(record);
            }
            await unitOfWork.SaveChangesAsync(ct);
            return records.Select(ToResponse).ToArray();
        }
        catch
        {
            // Database and object storage cannot share a transaction. Compensate on failure.
            foreach (var key in objects) await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
    }
    public Task<Page<FileResponse>> ListAsync(string ownerId, int page, int pageSize, CancellationToken ct) =>
        unitOfWork.Repository<StoredFile>().PageAsync(x => x.OwnerId == ownerId,
            x => new FileResponse(x.Id, x.OriginalName, x.Folder, x.ContentType, x.Length, x.CreatedAtUtc), page, pageSize, ct);

    public async Task<FileDownload> DownloadAsync(string ownerId, Guid id, CancellationToken ct)
    {
        var file = await unitOfWork.Repository<StoredFile>().GetAsync(id, ct: ct);
        if (file is null || file.OwnerId != ownerId) throw AppException.NotFound();
        return new(await storage.OpenReadAsync(file.StorageKey, ct), file.OriginalName, file.ContentType, file.Length);
    }
    public async Task DeleteAsync(string ownerId, Guid id, CancellationToken ct)
    {
        var repository = unitOfWork.Repository<StoredFile>();
        var file = await repository.GetAsync(id, tracking: true, ct);
        if (file is null || file.OwnerId != ownerId) throw AppException.NotFound();
        repository.Remove(file);
        await unitOfWork.SaveChangesAsync(ct);
        // Keep private bytes for the documented retention window; never expose storage keys.
    }
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
    private static FileResponse ToResponse(StoredFile x) => new(x.Id, x.OriginalName, x.Folder, x.ContentType, x.Length, x.CreatedAtUtc);
}
