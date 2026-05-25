using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Jellyfin.Plugin.CnNfo.Cache;
using Jellyfin.Plugin.CnNfo.Models;
using Jellyfin.Plugin.CnNfo.Util;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Api;

public class DoubanClient
{
    private const string MovieBase = "https://movie.douban.com";
    private const string SearchBase = "https://search.douban.com";
    private const string SuggestUrl = "https://movie.douban.com/j/subject_suggest?q=";

    private static readonly SemaphoreSlim ThrottleGate = new(1, 1);
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DoubanCookieJar _cookies;
    private readonly CnNfoCache _cache;
    private readonly ILogger<DoubanClient> _logger;
    private readonly HtmlParser _htmlParser = new();

    public DoubanClient(
        IHttpClientFactory httpClientFactory,
        DoubanCookieJar cookies,
        CnNfoCache cache,
        ILogger<DoubanClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cookies = cookies;
        _cache = cache;
        _logger = logger;
    }

    private HttpClient NewClient()
    {
        var client = _httpClientFactory.CreateClient("CnNfo.Douban");
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        var minIntervalMs = Math.Max(500, Plugin.Instance.Configuration.RequestIntervalMs);
        await ThrottleGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wait = _lastRequestAt + TimeSpan.FromMilliseconds(minIntervalMs) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, ct).ConfigureAwait(false);
            }
            _lastRequestAt = DateTimeOffset.UtcNow;
        }
        finally
        {
            ThrottleGate.Release();
        }
    }

    public async Task<IReadOnlyList<DoubanSearchEntry>> SearchAsync(
        string query,
        MediaCategory category,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<DoubanSearchEntry>();
        }

        var cacheKey = $"douban:search:{(int)category}:{query}";
        if (_cache.TryGet<IReadOnlyList<DoubanSearchEntry>>(cacheKey, out var hit) && hit is not null)
        {
            return hit;
        }

        var entries = new List<DoubanSearchEntry>();
        entries.AddRange(await SuggestAsync(query, ct).ConfigureAwait(false));

        // Suggest 命中很多时已够用；不够再走 HTML 搜索补充
        if (entries.Count < 3)
        {
            entries.AddRange(await SearchHtmlAsync(query, ct).ConfigureAwait(false));
        }

        var deduped = entries
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToList();

        _cache.Set<IReadOnlyList<DoubanSearchEntry>>(cacheKey, deduped, TimeSpan.FromMinutes(30));
        return deduped;
    }

    private async Task<IReadOnlyList<DoubanSearchEntry>> SuggestAsync(string query, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        var url = SuggestUrl + Uri.EscapeDataString(query);
        var client = NewClient();
        var body = await HttpUtil.SendWithRetryAsync(client, () =>
            HttpUtil.NewGet(url, MovieBase, _cookies.Current), _logger, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(body))
        {
            return Array.Empty<DoubanSearchEntry>();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var list = new List<DoubanSearchEntry>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var sub = item.TryGetProperty("sub_title", out var s) ? s.GetString() : null;
                var year = item.TryGetProperty("year", out var y) && int.TryParse(y.GetString(), out var yi) ? yi : (int?)null;
                var img = item.TryGetProperty("img", out var im) ? im.GetString() : null;
                var typeStr = item.TryGetProperty("type", out var ty) ? ty.GetString() : null;

                list.Add(new DoubanSearchEntry
                {
                    Id = id!,
                    Title = title,
                    Subtitle = sub,
                    Year = year,
                    PosterUrl = img,
                    // Suggest 端点不直接区分 movie / tv，category 为 Unknown，
                    // 在 SearchOrchestrator 里通过详情页二次判断。
                    Category = typeStr == "movie" ? MediaCategory.Unknown : MediaCategory.Unknown
                });
            }
            return list;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "CnNfo: douban suggest 返回非 JSON");
            return Array.Empty<DoubanSearchEntry>();
        }
    }

    private async Task<IReadOnlyList<DoubanSearchEntry>> SearchHtmlAsync(string query, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        // cat=1002 = 电影 + 电视剧 合并
        var url = $"{SearchBase}/movie/subject_search?search_text={Uri.EscapeDataString(query)}&cat=1002";
        var client = NewClient();
        var html = await HttpUtil.SendWithRetryAsync(client, () =>
            HttpUtil.NewGet(url, MovieBase, _cookies.Current), _logger, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
            return Array.Empty<DoubanSearchEntry>();
        }

        // search 页面的结果用 JS 注入，纯 HTML 抓不全；这里做 best-effort：
        // 提取 window.__DATA__ JSON
        var dataMatch = Regex.Match(html, @"window\.__DATA__\s*=\s*(\{.*?\});\s*</script>",
            RegexOptions.Singleline);
        if (!dataMatch.Success)
        {
            return Array.Empty<DoubanSearchEntry>();
        }

        try
        {
            using var doc = JsonDocument.Parse(dataMatch.Groups[1].Value);
            if (!doc.RootElement.TryGetProperty("items", out var items))
            {
                return Array.Empty<DoubanSearchEntry>();
            }

            var list = new List<DoubanSearchEntry>();
            foreach (var item in items.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl)
                    ? (idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64().ToString(CultureInfo.InvariantCulture) : idEl.GetString())
                    : null;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var img = item.TryGetProperty("cover_url", out var im) ? im.GetString() : null;
                var rating = item.TryGetProperty("rating", out var r)
                              && r.TryGetProperty("value", out var rv)
                              && rv.ValueKind == JsonValueKind.Number
                    ? rv.GetDouble() : (double?)null;
                var year = item.TryGetProperty("year", out var y) && y.ValueKind == JsonValueKind.Number
                    ? y.GetInt32() : (int?)null;
                var typeStr = item.TryGetProperty("type_name", out var tn) ? tn.GetString() : null;
                var category = typeStr switch
                {
                    "电影" => MediaCategory.Movie,
                    "电视剧" => MediaCategory.Series,
                    "动漫" => MediaCategory.Series,
                    _ => MediaCategory.Unknown
                };

                list.Add(new DoubanSearchEntry
                {
                    Id = id!,
                    Title = title,
                    Year = year,
                    Rating = rating,
                    PosterUrl = img,
                    Category = category
                });
            }
            return list;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "CnNfo: douban search 解析失败");
            return Array.Empty<DoubanSearchEntry>();
        }
    }

    public async Task<DoubanSubject?> GetSubjectAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        var cacheKey = $"douban:subject:{id}";
        return await _cache.GetOrAddAsync<DoubanSubject?>(cacheKey, () => FetchSubjectAsync(id, ct)).ConfigureAwait(false);
    }

    private async Task<DoubanSubject?> FetchSubjectAsync(string id, CancellationToken ct)
    {
        await ThrottleAsync(ct).ConfigureAwait(false);
        var url = $"{MovieBase}/subject/{id}/";
        var client = NewClient();
        var html = await HttpUtil.SendWithRetryAsync(client, () =>
            HttpUtil.NewGet(url, MovieBase, _cookies.Current), _logger, ct).ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }
        try
        {
            return await Parsers.DoubanHtmlParser.ParseSubjectAsync(_htmlParser, html, id, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CnNfo: 解析豆瓣详情失败 id={Id}", id);
            return null;
        }
    }
}
