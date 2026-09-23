namespace Sawa3ed.Application.Auth;

public sealed record UserResponse(string Id, string Email, string DisplayName, bool EmailConfirmed, IReadOnlyList<string> Roles);
