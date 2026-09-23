using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Infrastructure.Configuration;

public sealed class JwtOptions
{
    [Required] public string Issuer { get; set; } = "Sawa3ed";
    [Required] public string Audience { get; set; } = "Sawa3ed.Client";
    [Required, MinLength(64)] public string SigningKey { get; set; } = "";
    [Range(5, 30)] public int AccessMinutes { get; set; } = 10;
    [Range(1, 30)] public int RefreshDays { get; set; } = 7;
    [Required, MinLength(64)] public string OtpPepper { get; set; } = "";
}
public sealed class StorageOptions
{
    public string RootPath { get; set; } = "App_Data/uploads";
    [Range(1, 20 * 1024 * 1024)] public long MaxFileBytes { get; set; } = 10 * 1024 * 1024;
    [Range(1, 20)] public int MaxFiles { get; set; } = 10;
    [Range(1, 100 * 1024 * 1024)] public long MaxBatchBytes { get; set; } = 50 * 1024 * 1024;
}
public sealed class EmailOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "noreply@example.test";
    public bool UseDevelopmentPickup { get; set; }
    public string PickupPath { get; set; } = "App_Data/mail";
}
public sealed class ChatOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
    [Range(1, 30)] public int TimeoutSeconds { get; set; } = 25;
    [Range(1, 20)] public int MaxHistoryMessages { get; set; } = 12;
    [Range(100, 32000)] public int MaxContextCharacters { get; set; } = 16000;
    [Range(64, 2048)] public int MaxOutputTokens { get; set; } = 512;
}
