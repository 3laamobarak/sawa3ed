using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    [Required, StringLength(100, MinimumLength = 2)] string DisplayName);
public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);
public sealed record RefreshRequest([Required, StringLength(128, MinimumLength = 40)] string RefreshToken);
public enum OtpPurpose { ConfirmEmail = 1, ResetPassword = 2 }
public sealed record OtpRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [EnumDataType(typeof(OtpPurpose))] OtpPurpose Purpose);
public sealed record VerifyEmailRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);
public sealed record ResetPasswordRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, RegularExpression("^[0-9]{6}$")] string Code,
    [Required, MinLength(12), MaxLength(128)] string NewPassword);
public sealed record ChangePasswordRequest(
    [Required, MaxLength(128)] string CurrentPassword,
    [Required, MinLength(12), MaxLength(128)] string NewPassword);
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string RefreshToken, DateTime RefreshExpiresAtUtc);
public sealed record UserResponse(string Id, string Email, string DisplayName, bool EmailConfirmed, IReadOnlyList<string> Roles);
public sealed record RoleAssignmentRequest([Required, MaxLength(50)] string Role);
public sealed record RoleResponse(string Name, IReadOnlyList<string> Permissions);

public interface IAuthService
{
    Task RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<TokenResponse> RefreshAsync(string token, CancellationToken ct);
    Task RequestOtpAsync(OtpRequest request, CancellationToken ct);
    Task ConfirmEmailAsync(VerifyEmailRequest request, CancellationToken ct);
    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken ct);
    Task<UserResponse> MeAsync(string userId, CancellationToken ct);
    Task LogoutAsync(string userId, Guid sessionId, bool allSessions, CancellationToken ct);
}

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken ct);
    Task SetRoleAsync(string actorId, string userId, string role, bool remove, CancellationToken ct);
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string Parent = "Parent";
    public const string Support = "Support";
    public const string Supervisor = "Supervisor";
    public static readonly string[] All = [Admin, Student, Teacher, Parent, Support, Supervisor];
}

public static class Permissions
{
    public const string ManageRoles = "users.roles.manage";
    public const string UploadFiles = "files.upload";
    public const string Chat = "chat.use";
    public static string[] ForRole(string role) => role switch
    {
        Roles.Admin => [ManageRoles, UploadFiles, Chat],
        Roles.Teacher or Roles.Supervisor or Roles.Support => [UploadFiles, Chat],
        Roles.Student or Roles.Parent => [Chat],
        _ => []
    };
}
