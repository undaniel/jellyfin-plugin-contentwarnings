using Jellyfin.Plugin.ContentWarnings.Configuration;
using Jellyfin.Plugin.ContentWarnings.Dtdd;
using Jellyfin.Plugin.ContentWarnings.EventHandlers;
using Jellyfin.Plugin.ContentWarnings.Services;
using Jellyfin.Plugin.ContentWarnings.Storage;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings;

/// <summary>
/// Registers the plugin services.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient(DtddClient.HttpClientName, client =>
        {
            client.BaseAddress = DtddClient.BaseAddress;
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        serviceCollection.AddSingleton(serviceProvider => new WarningCache(
            Path.Combine(serviceProvider.GetRequiredService<IApplicationPaths>().DataPath, "contentwarnings"),
            serviceProvider.GetRequiredService<ILogger<WarningCache>>()));

        serviceCollection.AddSingleton<IDtddClient>(serviceProvider => new DtddClient(
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient(DtddClient.HttpClientName),
            () => Plugin.Instance?.Configuration ?? new PluginConfiguration(),
            serviceProvider.GetRequiredService<ILogger<DtddClient>>()));

        serviceCollection.AddSingleton<ContentWarningService>();
        serviceCollection.AddHostedService<LibraryEventsHandler>();
    }
}
