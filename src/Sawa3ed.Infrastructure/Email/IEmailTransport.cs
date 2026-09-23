namespace Sawa3ed.Infrastructure.Email;

public interface IEmailTransport
{
    Task SendAsync(OutboundEmail email, CancellationToken ct);
}
