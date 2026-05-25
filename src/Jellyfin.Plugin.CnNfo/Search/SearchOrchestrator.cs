using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Api;
using Jellyfin.Plugin.CnNfo.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Search;

/// <summary>
/// 三级 fallback：豆瓣 → IMDB(OMDb) → TMDB。
/// 同时按请求类型（Movie / Series）对豆瓣结果重排，
/// 避免请求《名侦探柯南》动画时命中同名剧场版。
/// </summary>
public class SearchOrchestrator
{
    private const int MaxDoubanCandidates = 5;

    private readonly DoubanClient _douban;
    private readonly ImdbClient _imdb;
    private readonly TmdbClient _tmdb;
    private readonly ILogger<SearchOrchestrator> _logger;

    public SearchOrchestrator(
        DoubanClient douban,
        ImdbClient imdb,
        TmdbClient tmdb,
        ILogger<SearchOrchestrator> logger)
    {
        _douban = douban;
        _imdb = imdb;
        _tmdb = tmdb;
        _logger = logger;
    }

    public async Task<DoubanSubject?> ResolveAsync(
        string query,
        int? year,
        MediaCategory wanted,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var fromDouban = await ResolveFromDoubanAsync(query, year, wanted, ct).ConfigureAwait(false);
        if (fromDouban is not null)
        {
            return fromDouban;
        }

        if (Plugin.Instance.Configuration.EnableImdbFallback && _imdb.IsConfigured)
        {
            var fromImdb = await _imdb.SearchAsync(query, year, wanted, ct).ConfigureAwait(false);
            if (fromImdb is not null)
            {
                return fromImdb;
            }
        }

        if (Plugin.Instance.Configuration.EnableTmdbFallback && _tmdb.IsConfigured)
        {
            return wanted == MediaCategory.Series
                ? await _tmdb.SearchSeriesAsync(query, year, ct).ConfigureAwait(false)
                : await _tmdb.SearchMovieAsync(query, year, ct).ConfigureAwait(false);
        }

        return null;
    }

    public async Task<IReadOnlyList<DoubanSearchEntry>> SearchAsync(
        string query,
        MediaCategory wanted,
        CancellationToken ct)
    {
        var entries = await _douban.SearchAsync(query, wanted, ct).ConfigureAwait(false);
        return SortByWantedCategory(entries, wanted);
    }

    private async Task<DoubanSubject?> ResolveFromDoubanAsync(
        string query,
        int? year,
        MediaCategory wanted,
        CancellationToken ct)
    {
        var entries = await _douban.SearchAsync(query, wanted, ct).ConfigureAwait(false);
        if (entries.Count == 0)
        {
            return null;
        }

        var sorted = SortByWantedCategory(entries, wanted)
            .Take(MaxDoubanCandidates)
            .ToList();

        DoubanSubject? firstAny = null;
        foreach (var entry in sorted)
        {
            ct.ThrowIfCancellationRequested();
            var subj = await _douban.GetSubjectAsync(entry.Id, ct).ConfigureAwait(false);
            if (subj is null)
            {
                continue;
            }
            firstAny ??= subj;

            if (wanted == MediaCategory.Unknown || subj.Category == wanted)
            {
                if (year is null || subj.Year is null || Math.Abs(subj.Year.Value - year.Value) <= 1)
                {
                    return subj;
                }
            }
        }
        return firstAny;
    }

    private static List<DoubanSearchEntry> SortByWantedCategory(
        IEnumerable<DoubanSearchEntry> entries,
        MediaCategory wanted)
    {
        if (wanted == MediaCategory.Unknown)
        {
            return entries.ToList();
        }

        return entries
            .Select((e, i) => (entry: e, idx: i))
            .OrderBy(t => t.entry.Category == wanted ? 0
                : t.entry.Category == MediaCategory.Unknown ? 1
                : 2)
            .ThenBy(t => t.idx)
            .Select(t => t.entry)
            .ToList();
    }
}
