using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record RoleAssignmentRequest([Required, MaxLength(50)] string Role);
