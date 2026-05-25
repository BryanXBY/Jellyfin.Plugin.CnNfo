using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.CnNfo.ExternalIds;

public class DoubanMovieExternalId : IExternalId
{
    public const string Provider = "Douban";

    public string ProviderName => Provider;

    public string Key => Provider;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    public bool Supports(IHasProviderIds item) => item is Movie;
}
