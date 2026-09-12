using Microsoft.Extensions.DependencyInjection;

namespace Dms.Client.Api;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers AuthSession, DmsApiClient, and DrowsinessHubClient.
    /// AuthSession's lifetime is the caller's choice: pass ServiceLifetime.Scoped
    /// in Blazor Server (one session per circuit) or ServiceLifetime.Singleton in
    /// a single-user WPF app.
    /// </summary>
    public static IServiceCollection AddDmsApiClient(
        this IServiceCollection services,
        Action<DmsApiOptions> configureOptions,
        ServiceLifetime authSessionLifetime = ServiceLifetime.Singleton)
    {
        services.Configure(configureOptions);
        services.Add(new ServiceDescriptor(typeof(AuthSession), typeof(AuthSession), authSessionLifetime));

        services.AddHttpClient<DmsApiClient>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<DmsApiOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        services.Add(new ServiceDescriptor(typeof(DrowsinessHubClient), typeof(DrowsinessHubClient), authSessionLifetime));

        return services;
    }
}
