using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.CnNfo.Providers;

/// <summary>
/// 单集 Provider：豆瓣没有 episode 级的公开页面，
/// 这里返回空让其他 provider（NFO / TVDB / TMDB-Episode）接手；
/// 只暴露 IRemoteMetadataProvider 接口让 Jellyfin 能正常解析季节。
/// </summary>
public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EpisodeProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ProviderHelpers.ProviderName;

    public Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        => Task.FromResult(new MetadataResult<Episode>());

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
}
