using Jellyfin.Plugin.CnNfo.Api;
using Jellyfin.Plugin.CnNfo.Cache;
using Jellyfin.Plugin.CnNfo.Parsers;
using Jellyfin.Plugin.CnNfo.Search;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.CnNfo;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddMemoryCache();

        serviceCollection.AddSingleton<CnNfoCache>();
        serviceCollection.AddSingleton<DoubanCookieJar>();
        serviceCollection.AddSingleton<DoubanClient>();
        serviceCollection.AddSingleton<TmdbClient>();
        serviceCollection.AddSingleton<ImdbClient>();
        serviceCollection.AddSingleton<FilenameParser>();
        serviceCollection.AddSingleton<TitleSplitter>();
        serviceCollection.AddSingleton<SearchOrchestrator>();
    }
}
