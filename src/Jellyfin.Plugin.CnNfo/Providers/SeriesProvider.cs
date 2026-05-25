using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Api;
using Jellyfin.Plugin.CnNfo.ExternalIds;
using Jellyfin.Plugin.CnNfo.Models;
using Jellyfin.Plugin.CnNfo.Search;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Providers;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
{
    private readonly SearchOrchestrator _orchestrator;
    private readonly DoubanClient _douban;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SeriesProvider> _logger;

    public SeriesProvider(
        SearchOrchestrator orchestrator,
        DoubanClient douban,
        IHttpClientFactory httpClientFactory,
        ILogger<SeriesProvider> logger)
    {
        _orchestrator = orchestrator;
        _douban = douban;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ProviderHelpers.ProviderName;

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();
        var doubanId = info.GetProviderId(DoubanMovieExternalId.Provider);
        DoubanSubject? subject = null;

        if (!string.IsNullOrEmpty(doubanId))
        {
            subject = await _douban.GetSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
        }
        subject ??= await _orchestrator.ResolveAsync(info.Name, info.Year, MediaCategory.Series, cancellationToken).ConfigureAwait(false);

        if (subject is null)
        {
            return result;
        }

        result.Item = new Series();
        ProviderHelpers.ApplyTo(result.Item, subject, Plugin.Instance.Configuration.PreferOriginalTitle);
        foreach (var p in ProviderHelpers.BuildPeople(subject))
        {
            result.AddPerson(p);
        }
        result.HasMetadata = true;
        result.QueriedById = !string.IsNullOrEmpty(doubanId);
        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var entries = await _orchestrator.SearchAsync(searchInfo.Name, MediaCategory.Series, cancellationToken).ConfigureAwait(false);
        return entries.Select(ProviderHelpers.ToSearchResult);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        => ProviderHelpers.FetchDoubanImageAsync(_httpClientFactory, url, cancellationToken);
}
