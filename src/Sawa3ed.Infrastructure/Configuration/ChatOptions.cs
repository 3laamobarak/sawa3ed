using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Infrastructure.Configuration;

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
