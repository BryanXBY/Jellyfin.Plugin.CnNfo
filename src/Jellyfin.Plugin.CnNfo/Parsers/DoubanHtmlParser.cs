using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using Jellyfin.Plugin.CnNfo.Models;

namespace Jellyfin.Plugin.CnNfo.Parsers;

internal static class DoubanHtmlParser
{
    public static async Task<DoubanSubject?> ParseSubjectAsync(
        HtmlParser parser,
        string html,
        string id,
        CancellationToken ct)
    {
        var doc = await parser.ParseDocumentAsync(html, ct).ConfigureAwait(false);

        var titleSpan = doc.QuerySelector("span[property='v:itemreviewed']")?.TextContent?.Trim();
        if (string.IsNullOrEmpty(titleSpan))
        {
            return null;
        }

        var info = doc.QuerySelector("#info")?.TextContent ?? string.Empty;
        var subject = new DoubanSubject
        {
            Id = id
        };

        var split = new TitleSplitter().Split(titleSpan);
        subject.Title = split.Chinese;
        subject.OriginalTitle = split.Original;

        if (int.TryParse(doc.QuerySelector(".year")?.TextContent?.Trim().Trim('(', ')'), out var year))
        {
            subject.Year = year;
        }

        if (double.TryParse(
            doc.QuerySelector("strong[property='v:average']")?.TextContent,
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var rating))
        {
            subject.Rating = rating;
        }

        subject.Genres = doc.QuerySelectorAll("span[property='v:genre']")
            .Select(s => s.TextContent.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        subject.Directors = doc.QuerySelectorAll("a[rel='v:directedBy']")
            .Select(a => a.TextContent.Trim())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToArray();

        subject.Cast = doc.QuerySelectorAll("a[rel='v:starring']")
            .Select(a => new DoubanCelebrity
            {
                Name = a.TextContent.Trim(),
                Id = TryExtractCelebId(a.GetAttribute("href")) ?? string.Empty
            })
            .Where(c => c.Name.Length > 0)
            .ToArray();

        subject.Overview = doc.QuerySelector("span[property='v:summary']")?.TextContent?.Trim();

        var poster = (doc.QuerySelector("#mainpic img") as IHtmlImageElement)?.Source;
        subject.PosterUrl = SanitizePosterUrl(poster);

        // info 块用正则提取
        subject.Aliases = ExtractInfoList(info, "又名");
        subject.Countries = ExtractInfoList(info, "制片国家/地区");
        subject.Languages = ExtractInfoList(info, "语言");

        var runtimeText = ExtractInfoSingle(info, "片长") ?? ExtractInfoSingle(info, "单集片长");
        if (!string.IsNullOrEmpty(runtimeText))
        {
            var m = Regex.Match(runtimeText, @"\d+");
            if (m.Success && int.TryParse(m.Value, out var minutes))
            {
                subject.RuntimeMinutes = minutes;
            }
        }

        var imdbLink = doc.QuerySelectorAll("#info a")
            .OfType<IHtmlAnchorElement>()
            .Select(a => a.Href)
            .FirstOrDefault(href => href != null && href.Contains("imdb.com/title/", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(imdbLink))
        {
            var im = Regex.Match(imdbLink, @"tt\d{6,}");
            if (im.Success)
            {
                subject.ImdbId = im.Value;
            }
        }

        var premiereText = ExtractInfoSingle(info, "上映日期") ?? ExtractInfoSingle(info, "首播");
        if (!string.IsNullOrEmpty(premiereText))
        {
            var dm = Regex.Match(premiereText, @"\d{4}-\d{1,2}(-\d{1,2})?");
            if (dm.Success && DateTimeOffset.TryParse(NormalizeDate(dm.Value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
            {
                subject.PremiereDate = dt;
                subject.Year ??= dt.Year;
            }
        }

        var seasonText = ExtractInfoSingle(info, "季数");
        if (!string.IsNullOrEmpty(seasonText) && int.TryParse(Regex.Match(seasonText, @"\d+").Value, out var s))
        {
            subject.SeasonNumber = s;
        }

        var episodesText = ExtractInfoSingle(info, "集数");
        if (!string.IsNullOrEmpty(episodesText) && int.TryParse(Regex.Match(episodesText, @"\d+").Value, out var e))
        {
            subject.EpisodeCount = e;
        }

        // 推断电影/剧集
        subject.Category = subject.EpisodeCount.HasValue || subject.SeasonNumber.HasValue
            ? MediaCategory.Series
            : MediaCategory.Movie;

        return subject;
    }

    private static string NormalizeDate(string s)
    {
        var parts = s.Split('-');
        return parts.Length switch
        {
            3 => $"{parts[0]}-{parts[1].PadLeft(2, '0')}-{parts[2].PadLeft(2, '0')}",
            2 => $"{parts[0]}-{parts[1].PadLeft(2, '0')}-01",
            _ => s
        };
    }

    private static IReadOnlyList<string> ExtractInfoList(string info, string label)
    {
        var pattern = $@"{Regex.Escape(label)}\s*[:：]\s*(.*?)(?:\r?\n|$)";
        var m = Regex.Match(info, pattern);
        if (!m.Success)
        {
            return Array.Empty<string>();
        }
        return m.Groups[1].Value
            .Split(new[] { '/', '、', ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();
    }

    private static string? ExtractInfoSingle(string info, string label)
    {
        var pattern = $@"{Regex.Escape(label)}\s*[:：]\s*(.*?)(?:\r?\n|$)";
        var m = Regex.Match(info, pattern);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? TryExtractCelebId(string? href)
    {
        if (string.IsNullOrEmpty(href))
        {
            return null;
        }
        var m = Regex.Match(href, @"/celebrity/(\d+)/");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string? SanitizePosterUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }
        // 把 s_ratio_poster 换成 l_ratio_poster 拿大图
        return url.Replace("/s_ratio_poster/", "/l_ratio_poster/", StringComparison.Ordinal)
                  .Replace("/spst/", "/lpst/", StringComparison.Ordinal);
    }
}
