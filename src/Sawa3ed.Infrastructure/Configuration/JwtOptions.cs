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
