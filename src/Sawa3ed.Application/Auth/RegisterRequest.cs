using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string DisplayName);
