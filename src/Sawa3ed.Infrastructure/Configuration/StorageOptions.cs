using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public string RootPath { get; set; } = "App_Data/uploads";
    [Range(1, 20 * 1024 * 1024)] public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    [Range(1, 20)] public int MaxFiles { get; set; } = 10;
    [Range(1, 100 * 1024 * 1024)] public long MaxBatchBytes { get; set; } = 50 * 1024 * 1024;
}
