using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Email;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Email;

internal static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailServices(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<EmailOptions>().BindConfiguration("Email")
            .Validate(x => environment.IsDevelopment() || environment.IsEnvironment("Testing") || !x.UseDevelopmentPickup, "Email pickup is restricted to Development/Testing.")
            .Validate(x => x.UseDevelopmentPickup || !string.IsNullOrWhiteSpace(x.Host), "Configure SMTP or development pickup.").ValidateOnStart();
        services.AddScoped<IEmailQueue, EmailQueue>();
        services.AddScoped<SmtpEmailTransport>();
        services.AddScoped<DevelopmentEmailTransport>();
        services.AddScoped<IEmailTransport>(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value.UseDevelopmentPickup
            ? sp.GetRequiredService<DevelopmentEmailTransport>()
            : sp.GetRequiredService<SmtpEmailTransport>());
        services.AddScoped<EmailOutboxProcessor>();
        services.AddHostedService<EmailDispatcher>();
        return services;
    }
}
