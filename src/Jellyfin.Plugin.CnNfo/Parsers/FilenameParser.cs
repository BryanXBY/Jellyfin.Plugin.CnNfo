using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnitomySharp;

namespace Jellyfin.Plugin.CnNfo.Parsers;

public class FilenameParser
{
    public class Result
    {
        public string Title { get; set; } = string.Empty;
        public int? Year { get; set; }
        public int? Season { get; set; }
        public int? Episode { get; set; }
        public string? ReleaseGroup { get; set; }
        public string? Resolution { get; set; }
    }

    public Result Parse(string path)
    {
        var fileName = Path.GetFileName(path) ?? path;
        IEnumerable<Element> elements;
        try
        {
            elements = AnitomySharp.AnitomySharp.Parse(fileName);
        }
        catch
        {
            return new Result { Title = StripExt(fileName) };
        }

        string Get(Element.ElementCategory cat)
            => elements.FirstOrDefault(e => e.Category == cat)?.Value?.Trim() ?? string.Empty;

        var r = new Result
        {
            Title = Get(Element.ElementCategory.ElementAnimeTitle),
            ReleaseGroup = Get(Element.ElementCategory.ElementReleaseGroup),
            Resolution = Get(Element.ElementCategory.ElementVideoResolution),
        };

        var yearStr = Get(Element.ElementCategory.ElementAnimeYear);
        if (int.TryParse(yearStr, out var y))
        {
            r.Year = y;
        }
        var epStr = Get(Element.ElementCategory.ElementEpisodeNumber);
        if (int.TryParse(epStr, out var ep))
        {
            r.Episode = ep;
        }
        // Anitomy 没有 ElementSeasonNumber 的直接 enum，按 v 处理或留空
        // 季可能在 ElementAnimeSeason
        var seasonStr = elements.FirstOrDefault(e =>
            e.Category.ToString().Equals("ElementAnimeSeason", StringComparison.Ordinal))?.Value;
        if (int.TryParse(seasonStr, out var sn))
        {
            r.Season = sn;
        }

        if (string.IsNullOrEmpty(r.Title))
        {
            r.Title = StripExt(fileName);
        }

        return r;
    }

    private static string StripExt(string name) => Path.GetFileNameWithoutExtension(name);
}
