namespace Sawa3ed.Application.Auth;

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
