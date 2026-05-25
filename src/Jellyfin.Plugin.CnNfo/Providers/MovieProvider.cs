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
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Providers;

public class MovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>
{
    private readonly SearchOrchestrator _orchestrator;
    private readonly DoubanClient _douban;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MovieProvider> _logger;

    public MovieProvider(
        SearchOrchestrator orchestrator,
        DoubanClient douban,
        IHttpClientFactory httpClientFactory,
        ILogger<MovieProvider> logger)
    {
        _orchestrator = orchestrator;
        _douban = douban;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => ProviderHelpers.ProviderName;

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie>();
        var doubanId = info.GetProviderId(DoubanMovieExternalId.Provider);
        DoubanSubject? subject = null;

        if (!string.IsNullOrEmpty(doubanId))
        {
            subject = await _douban.GetSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
        }

        subject ??= await _orchestrator.ResolveAsync(info.Name, info.Year, MediaCategory.Movie, cancellationToken).ConfigureAwait(false);

        if (subject is null)
        {
            return result;
        }

        result.Item = new Movie();
        ProviderHelpers.ApplyTo(result.Item, subject, Plugin.Instance.Configuration.PreferOriginalTitle);
        foreach (var p in ProviderHelpers.BuildPeople(subject))
        {
            result.AddPerson(p);
        }
        result.HasMetadata = true;
        result.QueriedById = !string.IsNullOrEmpty(doubanId);
        return result;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
    {
        var doubanId = searchInfo.GetProviderId(DoubanMovieExternalId.Provider);
        if (!string.IsNullOrEmpty(doubanId))
        {
            var subj = await _douban.GetSubjectAsync(doubanId, cancellationToken).ConfigureAwait(false);
            if (subj is not null)
            {
                return new[] { ProviderHelpers.ToSearchResult(new DoubanSearchEntry
                {
                    Id = subj.Id,
                    Title = subj.Title,
                    Year = subj.Year,
                    PosterUrl = subj.PosterUrl,
                    Category = MediaCategory.Movie
                }) };
            }
        }

        var entries = await _orchestrator.SearchAsync(searchInfo.Name, MediaCategory.Movie, cancellationToken).ConfigureAwait(false);
        return entries.Select(ProviderHelpers.ToSearchResult);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        return client.GetAsync(url, cancellationToken);
    }
}
