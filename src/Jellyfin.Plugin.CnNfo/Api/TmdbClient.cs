using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Cache;
using Jellyfin.Plugin.CnNfo.Models;
using Microsoft.Extensions.Logging;
using TMDbLib.Client;
using TMDbLib.Objects.General;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

namespace Jellyfin.Plugin.CnNfo.Api;

public class TmdbClient
{
    private readonly CnNfoCache _cache;
    private readonly ILogger<TmdbClient> _logger;

    public TmdbClient(CnNfoCache cache, ILogger<TmdbClient> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    private TMDbClient? Build()
    {
        var key = Plugin.Instance.Configuration.TmdbApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        return new TMDbClient(key)
        {
            DefaultLanguage = Plugin.Instance.Configuration.PreferredLanguage,
            DefaultCountry = Plugin.Instance.Configuration.PreferredCountry
        };
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.TmdbApiKey);

    public async Task<DoubanSubject?> SearchMovieAsync(string query, int? year, CancellationToken ct)
    {
        var client = Build();
        if (client is null)
        {
            return null;
        }
        var cacheKey = $"tmdb:movie:{year}:{query}";
        return await _cache.GetOrAddAsync<DoubanSubject?>(cacheKey, async () =>
        {
            try
            {
                var search = await client.SearchMovieAsync(query, 0, false, year ?? 0, null, cancellationToken: ct).ConfigureAwait(false);
                var hit = search.Results.FirstOrDefault();
                if (hit is null)
                {
                    return null;
                }
                var detail = await client.GetMovieAsync(hit.Id, MovieMethods.Credits, ct).ConfigureAwait(false);
                return Map(detail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CnNfo: TMDB movie 搜索失败 {Query}", query);
                return null;
            }
        }).ConfigureAwait(false);
    }

    public async Task<DoubanSubject?> SearchSeriesAsync(string query, int? year, CancellationToken ct)
    {
        var client = Build();
        if (client is null)
        {
            return null;
        }
        var cacheKey = $"tmdb:tv:{year}:{query}";
        return await _cache.GetOrAddAsync<DoubanSubject?>(cacheKey, async () =>
        {
            try
            {
                var search = await client.SearchTvShowAsync(query, 0, false, year ?? 0, cancellationToken: ct).ConfigureAwait(false);
                var hit = search.Results.FirstOrDefault();
                if (hit is null)
                {
                    return null;
                }
                var detail = await client.GetTvShowAsync(hit.Id, TvShowMethods.Credits, cancellationToken: ct).ConfigureAwait(false);
                return Map(detail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CnNfo: TMDB tv 搜索失败 {Query}", query);
                return null;
            }
        }).ConfigureAwait(false);
    }

    private static DoubanSubject Map(Movie movie)
    {
        return new DoubanSubject
        {
            Id = "tmdb:" + movie.Id,
            Title = movie.Title ?? movie.OriginalTitle ?? string.Empty,
            OriginalTitle = movie.OriginalTitle,
            Year = movie.ReleaseDate?.Year,
            PremiereDate = movie.ReleaseDate.HasValue ? new DateTimeOffset(movie.ReleaseDate.Value) : null,
            Rating = movie.VoteAverage,
            VoteCount = movie.VoteCount,
            Genres = movie.Genres?.Select(g => g.Name).ToArray() ?? Array.Empty<string>(),
            Overview = movie.Overview,
            PosterUrl = movie.PosterPath is null ? null : "https://image.tmdb.org/t/p/original" + movie.PosterPath,
            BackdropUrl = movie.BackdropPath is null ? null : "https://image.tmdb.org/t/p/original" + movie.BackdropPath,
            ImdbId = movie.ImdbId,
            RuntimeMinutes = movie.Runtime,
            Countries = movie.ProductionCountries?.Select(c => c.Name).ToArray() ?? Array.Empty<string>(),
            Directors = movie.Credits?.Crew?.Where(c => c.Job == "Director").Select(c => c.Name).ToArray() ?? Array.Empty<string>(),
            Cast = movie.Credits?.Cast?.Select(c => new DoubanCelebrity
            {
                Id = "tmdb:" + c.Id,
                Name = c.Name,
                Role = c.Character,
                PhotoUrl = c.ProfilePath is null ? null : "https://image.tmdb.org/t/p/w300" + c.ProfilePath
            }).ToArray() ?? Array.Empty<DoubanCelebrity>(),
            Category = MediaCategory.Movie
        };
    }

    private static DoubanSubject Map(TvShow tv)
    {
        return new DoubanSubject
        {
            Id = "tmdb:" + tv.Id,
            Title = tv.Name ?? tv.OriginalName ?? string.Empty,
            OriginalTitle = tv.OriginalName,
            Year = tv.FirstAirDate?.Year,
            PremiereDate = tv.FirstAirDate.HasValue ? new DateTimeOffset(tv.FirstAirDate.Value) : null,
            Rating = tv.VoteAverage,
            VoteCount = tv.VoteCount,
            Genres = tv.Genres?.Select(g => g.Name).ToArray() ?? Array.Empty<string>(),
            Overview = tv.Overview,
            PosterUrl = tv.PosterPath is null ? null : "https://image.tmdb.org/t/p/original" + tv.PosterPath,
            BackdropUrl = tv.BackdropPath is null ? null : "https://image.tmdb.org/t/p/original" + tv.BackdropPath,
            Countries = tv.OriginCountry?.ToArray() ?? Array.Empty<string>(),
            EpisodeCount = tv.NumberOfEpisodes > 0 ? tv.NumberOfEpisodes : null,
            Directors = tv.CreatedBy?.Select(c => c.Name).ToArray() ?? Array.Empty<string>(),
            Cast = tv.Credits?.Cast?.Select(c => new DoubanCelebrity
            {
                Id = "tmdb:" + c.Id,
                Name = c.Name,
                Role = c.Character,
                PhotoUrl = c.ProfilePath is null ? null : "https://image.tmdb.org/t/p/w300" + c.ProfilePath
            }).ToArray() ?? Array.Empty<DoubanCelebrity>(),
            Category = MediaCategory.Series
        };
    }
}
