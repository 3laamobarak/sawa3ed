namespace Sawa3ed.Application.Common;

public sealed class AppException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public static AppException Invalid(string message) => new(400, "invalid_request", message);
    public static AppException Unauthorized() => new(401, "invalid_credentials", "Invalid credentials or expired verification.");
    public static AppException NotFound() => new(404, "not_found", "The requested resource was not found.");
}
