using Microsoft.Extensions.Options;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Email;

public sealed class DevelopmentEmailTransport(IOptions<EmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(OutboundEmail email, CancellationToken ct)
    {
        var settings = options.Value;
        using var message = EmailMessageFactory.Create(email, settings.From);
        Directory.CreateDirectory(settings.PickupPath);
        await message.WriteToAsync(Path.Combine(settings.PickupPath, email.Id.ToString("N") + ".eml"), ct);
    }
}
