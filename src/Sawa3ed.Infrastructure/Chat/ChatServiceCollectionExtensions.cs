using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Chat;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Chat;

internal static class ChatServiceCollectionExtensions
{
    public static IServiceCollection AddChatProvider(this IServiceCollection services)
    {
        services.AddOptions<ChatOptions>().BindConfiguration("Chat").ValidateDataAnnotations()
            .Validate(x => !x.Enabled || (!string.IsNullOrWhiteSpace(x.ApiKey) && !string.IsNullOrWhiteSpace(x.Model) &&
                Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var uri) && uri.Scheme == "https" && string.IsNullOrEmpty(uri.UserInfo)),
                "Enabled chat requires a model, API key, and HTTPS base URL.").ValidateOnStart();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<ChatOptions>>().Value;
            return new ChatContextLimits(settings.MaxHistoryMessages, settings.MaxContextCharacters);
        });
        services.AddHttpClient<IChatClient, CompatibleChatClient>(client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5), MaxConnectionsPerServer = 20
            });
        return services;
    }
}
