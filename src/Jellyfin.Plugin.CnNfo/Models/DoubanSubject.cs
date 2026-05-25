using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.CnNfo.Models;

public class DoubanSubject
{
    public string Id { get; set; } = string.Empty;

    /// <summary>中文标题（豆瓣主标题）。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>原名 / 外语原标题。</summary>
    public string? OriginalTitle { get; set; }

    public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();

    public int? Year { get; set; }

    public DateTimeOffset? PremiereDate { get; set; }

    public double? Rating { get; set; }

    public int? VoteCount { get; set; }

    public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Countries { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Languages { get; set; } = Array.Empty<string>();

    public int? RuntimeMinutes { get; set; }

    public string? Overview { get; set; }

    public string? PosterUrl { get; set; }

    public string? BackdropUrl { get; set; }

    public IReadOnlyList<string> Directors { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Writers { get; set; } = Array.Empty<string>();

    public IReadOnlyList<DoubanCelebrity> Cast { get; set; } = Array.Empty<DoubanCelebrity>();

    public string? ImdbId { get; set; }

    public MediaCategory Category { get; set; } = MediaCategory.Unknown;

    public int? EpisodeCount { get; set; }

    public int? SeasonNumber { get; set; }
}
