using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(12), MaxLength(128)] string NewPassword);
