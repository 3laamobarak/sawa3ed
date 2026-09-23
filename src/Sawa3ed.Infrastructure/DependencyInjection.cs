using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sawa3ed.Infrastructure.Chat;
using Sawa3ed.Infrastructure.Email;
using Sawa3ed.Infrastructure.Identity;
using Sawa3ed.Infrastructure.Persistence;
using Sawa3ed.Infrastructure.Storage;

namespace Sawa3ed.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddPersistence();
        services.AddIdentityServices();
        services.AddEmailServices(environment);
        services.AddFileStorage();
        services.AddChatProvider();
        return services;
    }
}
