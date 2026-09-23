using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Email;

public sealed class SmtpEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(OutboundEmail email, CancellationToken ct)
    {
        var settings = options.Value;
        using var message = EmailMessageFactory.Create(email, settings.From);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(settings.Host, settings.Port,
            settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, timeout.Token);
        if (!string.IsNullOrWhiteSpace(settings.UserName))
            await smtp.AuthenticateAsync(settings.UserName, settings.Password, timeout.Token);
        await smtp.SendAsync(message, timeout.Token);
        await smtp.DisconnectAsync(true, timeout.Token);
    }
}
