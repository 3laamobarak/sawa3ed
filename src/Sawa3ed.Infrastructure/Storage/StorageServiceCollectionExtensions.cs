using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sawa3ed.Application.Files;
using Sawa3ed.Infrastructure.Configuration;

namespace Sawa3ed.Infrastructure.Storage;

internal static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddFileStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>().BindConfiguration("Storage").ValidateDataAnnotations().ValidateOnStart();
        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return new FileUploadLimits(settings.MaxFiles, settings.MaxBatchBytes);
        });
        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IUploadScanner, UploadScanner>();
        return services;
    }
}
