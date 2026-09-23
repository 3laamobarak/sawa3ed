using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);
