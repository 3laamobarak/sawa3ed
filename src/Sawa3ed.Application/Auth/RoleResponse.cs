namespace Sawa3ed.Application.Auth;

public sealed record RoleResponse(string Name, IReadOnlyList<string> Permissions);
