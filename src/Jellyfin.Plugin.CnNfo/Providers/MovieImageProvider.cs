using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Api;
using Jellyfin.Plugin.CnNfo.ExternalIds;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.CnNfo.Providers;

public class MovieImageProvider : IRemoteImageProvider
{
    private readonly DoubanClient _douban;
    private readonly IHttpClientFactory _httpClientFactory;

    public MovieImageProvider(DoubanClient douban, IHttpClientFactory httpClientFactory)
    {
        _douban = douban;
        _httpClientFactory = httpClientFactory;
    }

    public string Name => ProviderHelpers.ProviderName;

    public bool Supports(BaseItem item) => item is Movie;

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[]
    {
        ImageType.Primary,
        ImageType.Backdrop
    };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var doubanId = item.GetProviderId(DoubanMovieExternalId.Provider);
        if (string.IsNullOrEmpty(doubanId))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }
        var subj = await _douban.GetSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
        return subj is null ? Enumerable.Empty<RemoteImageInfo>() : ProviderHelpers.ToImageInfos(subj);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
}
