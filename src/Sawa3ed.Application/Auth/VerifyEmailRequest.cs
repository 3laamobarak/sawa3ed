using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record VerifyEmailRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);
