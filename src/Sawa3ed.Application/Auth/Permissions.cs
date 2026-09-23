namespace Sawa3ed.Application.Auth;

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
