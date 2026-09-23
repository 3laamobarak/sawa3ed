namespace Sawa3ed.Application.Chat;

public sealed record ChatMessageResponse(Guid Id, string Role, string Content, DateTime CreatedAtUtc);
