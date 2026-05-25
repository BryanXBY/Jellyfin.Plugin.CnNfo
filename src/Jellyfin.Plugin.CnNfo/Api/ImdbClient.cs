using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Cache;
using Jellyfin.Plugin.CnNfo.Models;
using Jellyfin.Plugin.CnNfo.Util;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Api;

/// <summary>
/// 通过 OMDb API（http://www.omdbapi.com）做 IMDB 兜底。
/// 选 OMDb 是因为 IMDb 官方搜索接口没有公开 key-less 入口。
/// </summary>
public class ImdbClient
{
    private const string OmdbBase = "https://www.omdbapi.com/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CnNfoCache _cache;
    private readonly ILogger<ImdbClient> _logger;

    public ImdbClient(IHttpClientFactory httpClientFactory, CnNfoCache cache, ILogger<ImdbClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Plugin.Instance.Configuration.OmdbApiKey);

    public async Task<DoubanSubject?> SearchAsync(string title, int? year, MediaCategory category, CancellationToken ct)
    {
        var apiKey = Plugin.Instance.Configuration.OmdbApiKey;
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var cacheKey = $"omdb:{category}:{year}:{title}";
        return await _cache.GetOrAddAsync<DoubanSubject?>(cacheKey, async () =>
        {
            var url = $"{OmdbBase}?apikey={Uri.EscapeDataString(apiKey)}&t={Uri.EscapeDataString(title)}";
            if (year is { } y)
            {
                url += "&y=" + y;
            }
            if (category == MediaCategory.Movie)
            {
                url += "&type=movie";
            }
            else if (category == MediaCategory.Series)
            {
                url += "&type=series";
            }

            var client = _httpClientFactory.CreateClient("CnNfo.Omdb");
            client.Timeout = TimeSpan.FromSeconds(20);
            var body = await HttpUtil.SendWithRetryAsync(client, () => HttpUtil.NewGet(url), _logger, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(body))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("Response", out var resp) && resp.GetString() == "False")
                {
                    return null;
                }

                var s = new DoubanSubject
                {
                    Id = "imdb:" + (root.TryGetProperty("imdbID", out var iid) ? iid.GetString() ?? string.Empty : string.Empty),
                    Title = root.TryGetProperty("Title", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                    OriginalTitle = root.TryGetProperty("Title", out var t2) ? t2.GetString() : null,
                    Overview = root.TryGetProperty("Plot", out var p) ? p.GetString() : null,
                    PosterUrl = root.TryGetProperty("Poster", out var ps) ? ps.GetString() : null,
                    ImdbId = root.TryGetProperty("imdbID", out var iid2) ? iid2.GetString() : null,
                    Category = category
                };

                if (root.TryGetProperty("Year", out var ye))
                {
                    var ystr = ye.GetString();
                    if (!string.IsNullOrEmpty(ystr) && int.TryParse(ystr[..Math.Min(4, ystr.Length)], out var yi))
                    {
                        s.Year = yi;
                    }
                }
                if (root.TryGetProperty("imdbRating", out var ra)
                    && double.TryParse(ra.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var rd))
                {
                    s.Rating = rd;
                }
                if (root.TryGetProperty("Runtime", out var ru))
                {
                    var rs = ru.GetString() ?? string.Empty;
                    var m = System.Text.RegularExpressions.Regex.Match(rs, @"\d+");
                    if (m.Success && int.TryParse(m.Value, out var rmin))
                    {
                        s.RuntimeMinutes = rmin;
                    }
                }
                if (root.TryGetProperty("Genre", out var ge))
                {
                    s.Genres = (ge.GetString() ?? string.Empty)
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .ToArray();
                }
                return s;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "CnNfo: OMDb JSON 解析失败");
                return null;
            }
        }).ConfigureAwait(false);
    }
}
