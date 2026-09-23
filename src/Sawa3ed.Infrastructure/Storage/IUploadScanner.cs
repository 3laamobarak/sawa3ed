namespace Sawa3ed.Infrastructure.Storage;

public interface IUploadScanner
{
    Task ScanAsync(string path, CancellationToken ct);
}
