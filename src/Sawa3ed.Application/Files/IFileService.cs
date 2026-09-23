using Sawa3ed.Application.Abstractions;

namespace Sawa3ed.Application.Files;

public interface IFileService
{
    Task<IReadOnlyList<FileResponse>> UploadAsync(string ownerId, IReadOnlyList<UploadFile> files, CancellationToken ct);
    Task<Page<FileResponse>> ListAsync(string ownerId, int page, int pageSize, CancellationToken ct);
    Task<FileDownload> DownloadAsync(string ownerId, Guid id, CancellationToken ct);
    Task DeleteAsync(string ownerId, Guid id, CancellationToken ct);
}
