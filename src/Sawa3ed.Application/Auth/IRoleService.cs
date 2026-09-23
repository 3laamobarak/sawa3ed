namespace Sawa3ed.Application.Auth;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct);
    Task SetRoleAsync(string actorId, string userId, string role, bool remove, CancellationToken ct);
}
