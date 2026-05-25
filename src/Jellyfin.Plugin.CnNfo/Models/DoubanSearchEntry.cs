namespace Jellyfin.Plugin.CnNfo.Models;

public class DoubanSearchEntry
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>豆瓣的副标题（通常包含原名 + 其他译名）。</summary>
    public string? Subtitle { get; set; }

    public int? Year { get; set; }

    /// <summary>豆瓣页面顶部分类标签：'电影' / '电视剧' / 其他。</summary>
    public MediaCategory Category { get; set; } = MediaCategory.Unknown;

    public double? Rating { get; set; }

    public string? PosterUrl { get; set; }
}
