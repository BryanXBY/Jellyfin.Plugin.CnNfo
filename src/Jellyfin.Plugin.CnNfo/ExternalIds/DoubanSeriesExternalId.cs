using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.CnNfo.ExternalIds;

public class DoubanSeriesExternalId : IExternalId
{
    public string ProviderName => DoubanMovieExternalId.Provider;

    public string Key => DoubanMovieExternalId.Provider;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    public string? UrlFormatString => "https://movie.douban.com/subject/{0}/";

    public bool Supports(IHasProviderIds item) => item is Series;
}
