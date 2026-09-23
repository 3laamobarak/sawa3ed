using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record OtpRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [EnumDataType(typeof(OtpPurpose))] OtpPurpose Purpose);
