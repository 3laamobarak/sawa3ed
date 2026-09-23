using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record RefreshRequest([Required, StringLength(128, MinimumLength = 40)] string RefreshToken);
