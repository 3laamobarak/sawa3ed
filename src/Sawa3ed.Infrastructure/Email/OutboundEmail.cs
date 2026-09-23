namespace Sawa3ed.Infrastructure.Email;

public sealed record OutboundEmail(Guid Id, string To, string Subject, string Body);
