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
/// 季级 Provider：豆瓣条目通常与 Series 一一对应，这里只透传名字与年份。
/// 实际剧集/季的具体集列表豆瓣不开放，留待用户 NFO 或其他 provider 补齐。
/// </summary>
public class SeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SeasonProvider(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ProviderHelpers.ProviderName;

    public Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        => Task.FromResult(new MetadataResult<Season>());

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        => Task.FromResult(Enumerable.Empty<RemoteSearchResult>());

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => ProviderHelpers.FetchDoubanImageAsync(_httpClientFactory, url, cancellationToken);
}
