using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sawa3ed.Application.Common;

namespace Sawa3ed.Infrastructure.Storage;

// ClamAV INSTREAM: bytes never leave the configured private scanning service.
public sealed class UploadScanner(IConfiguration configuration, IHostEnvironment environment)
{
    public async Task ScanAsync(string path, CancellationToken ct)
    {
        var host = configuration["Scanning:Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return;
            throw new AppException(503, "scanner_unavailable", "File scanning is unavailable.");
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, configuration.GetValue("Scanning:Port", 3310), timeout.Token);
            await using var network = client.GetStream();
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
            await using var file = File.OpenRead(path);
            var buffer = new byte[65536];
            var size = new byte[4];
            int read;
            while ((read = await file.ReadAsync(buffer, timeout.Token)) > 0)
            {
                BinaryPrimitives.WriteInt32BigEndian(size, read);
                await network.WriteAsync(size, timeout.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            await network.WriteAsync(new byte[4], timeout.Token);
            var response = new byte[1024];
            var count = 0;
            while (count < response.Length)
            {
                read = await network.ReadAsync(response.AsMemory(count), timeout.Token);
                if (read == 0) break;
                count += read;
                if (response.AsSpan(0, count).Contains((byte)0)) break;
            }
            var result = Encoding.UTF8.GetString(response, 0, count).TrimEnd('\0', '\n');
            if (result.EndsWith(" FOUND", StringComparison.Ordinal)) throw AppException.Invalid("The file failed the security scan.");
            if (result != "stream: OK") throw new IOException("Scanner did not confirm a clean file.");
        }
        catch (AppException) { throw; }
        catch (Exception ex) when (ex is IOException or SocketException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new AppException(503, "scanner_unavailable", "File scanning is unavailable. Try again later.");
        }
    }
}
