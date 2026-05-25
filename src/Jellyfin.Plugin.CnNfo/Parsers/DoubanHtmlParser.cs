using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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

        subject.Overview = CleanOverview(ExtractOverview(doc));

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

    /// <summary>
    /// 豆瓣详情页的剧情简介通常有两个 span：
    ///   - .all (hidden) -> 完整长版本
    ///   - .short        -> 截断版 + "(展开全部)" 链接
    /// 两个都可能带 property="v:summary"，所以单纯 QuerySelector 容易取到 .short。
    /// 这里显式优先取 .all，再回退到 [property=v:summary]，最后挑最长那段。
    /// </summary>
    private static string? ExtractOverview(AngleSharp.Dom.IDocument doc)
    {
        var candidates = new List<string?>
        {
            doc.QuerySelector("#link-report-intra span.all")?.TextContent,
            doc.QuerySelector("#link-report span.all")?.TextContent,
            doc.QuerySelector("span.all[property='v:summary']")?.TextContent,
            doc.QuerySelector("span[property='v:summary']:not(.short)")?.TextContent,
            doc.QuerySelector("span[property='v:summary']")?.TextContent,
            doc.QuerySelector("#link-report .short")?.TextContent
        };
        return candidates
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderByDescending(s => s!.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// 豆瓣简介里常有：
    ///   - 段落前缀的全角空格 "　　"
    ///   - 行首大段半角空格（HTML 缩进残留）
    ///   - 多个连续空行
    ///   - 末尾 "(展开全部)" / "(收起)" 控制文本
    /// 这里把这些都清掉，保留段落换行。
    /// </summary>
    private static string CleanOverview(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        // 去掉 "(展开全部)" / "(收起)" / "©豆瓣" 之类的 UI 标记
        var cleaned = Regex.Replace(raw, @"\(\s*展开全部\s*\)|\(\s*收起\s*\)|©\s*豆瓣", string.Empty);

        var lines = cleaned.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var paragraphs = new List<string>();
        var cur = new StringBuilder();

        foreach (var rawLine in lines)
        {
            // 去前后半角空格、全角空格、制表符
            var line = rawLine.Trim().Trim('\t', ' ', '　');
            if (line.Length == 0)
            {
                if (cur.Length > 0)
                {
                    paragraphs.Add(cur.ToString());
                    cur.Clear();
                }
                continue;
            }
            if (cur.Length > 0)
            {
                cur.Append('\n');
            }
            cur.Append(line);
        }
        if (cur.Length > 0)
        {
            paragraphs.Add(cur.ToString());
        }

        return string.Join("\n\n", paragraphs);
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
