using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Application.Files;

public sealed record UploadFile(string Name, string? RelativePath, long Length, Stream Content);
public sealed record FileResponse(Guid Id, string Name, string Folder, string ContentType, long Length, DateTime CreatedAtUtc);
public sealed record FileDownload(Stream Content, string Name, string ContentType, long Length);
public sealed record StoredObject(string Key, string ContentType, long Length, string Sha256);

public interface IFileStorage
{
    Task<StoredObject> WriteAsync(UploadFile file, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
public interface IFileService
{
    Task<IReadOnlyList<FileResponse>> UploadAsync(string ownerId, IReadOnlyList<UploadFile> files, CancellationToken ct);
    Task<Page<FileResponse>> ListAsync(string ownerId, int page, int pageSize, CancellationToken ct);
    Task<FileDownload> DownloadAsync(string ownerId, Guid id, CancellationToken ct);
    Task DeleteAsync(string ownerId, Guid id, CancellationToken ct);
}
