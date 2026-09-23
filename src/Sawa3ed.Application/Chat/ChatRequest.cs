using System.ComponentModel.DataAnnotations;

namespace Sawa3ed.Application.Chat;

public sealed record ChatRequest(
    [Required, StringLength(4000, MinimumLength = 1)] string Message,
    Guid? ConversationId = null);
