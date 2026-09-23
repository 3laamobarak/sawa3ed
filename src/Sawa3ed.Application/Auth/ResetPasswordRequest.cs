using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record ResetPasswordRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, MinLength(12), MaxLength(128)] string NewPassword);
