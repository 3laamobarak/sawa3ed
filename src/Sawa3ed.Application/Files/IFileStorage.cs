namespace Sawa3ed.Application.Files;

public interface IFileStorage
{
    Task<StoredObject> WriteAsync(UploadFile file, CancellationToken ct);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
