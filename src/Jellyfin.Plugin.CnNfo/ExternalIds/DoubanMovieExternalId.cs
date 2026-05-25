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

    // 10.11.0 早期发布版（如 TerraMaster TOS 自编译 c8eed9a89）IExternalId 仍要求
    // UrlFormatString 作为抽象成员；后续版本改为 DIM/移除。这里显式实现保持兼容。
    public string? UrlFormatString => "https://movie.douban.com/subject/{0}/";

    public bool Supports(IHasProviderIds item) => item is Movie;
}
