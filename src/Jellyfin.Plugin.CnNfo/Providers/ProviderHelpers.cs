using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.CnNfo.ExternalIds;
using Jellyfin.Plugin.CnNfo.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.CnNfo.Providers;

internal static class ProviderHelpers
{
    public const string ProviderName = "CnNfo";

    /// <summary>把 DoubanSubject 拷贝到目标 BaseItem。</summary>
    public static void ApplyTo(BaseItem item, DoubanSubject subj, bool preferOriginalTitle)
    {
        var primary = preferOriginalTitle && !string.IsNullOrEmpty(subj.OriginalTitle)
            ? subj.OriginalTitle!
            : subj.Title;

        var secondary = preferOriginalTitle ? subj.Title : subj.OriginalTitle;

        item.Name = primary;
        if (!string.IsNullOrEmpty(secondary))
        {
            item.OriginalTitle = secondary;
        }

        if (subj.PremiereDate is { } d)
        {
            item.PremiereDate = d.UtcDateTime;
        }
        if (subj.Year is { } y)
        {
            item.ProductionYear = y;
        }
        if (subj.Rating is { } r)
        {
            item.CommunityRating = (float)r;
        }
        if (subj.RuntimeMinutes is { } rm)
        {
            item.RunTimeTicks = rm * System.TimeSpan.TicksPerMinute;
        }
        if (!string.IsNullOrEmpty(subj.Overview))
        {
            item.Overview = subj.Overview;
        }
        if (subj.Genres.Count > 0)
        {
            item.Genres = subj.Genres.ToArray();
        }
        if (subj.Countries.Count > 0)
        {
            item.ProductionLocations = subj.Countries.ToArray();
        }

        if (!subj.Id.StartsWith("tmdb:") && !subj.Id.StartsWith("imdb:") && !string.IsNullOrEmpty(subj.Id))
        {
            item.SetProviderId(DoubanMovieExternalId.Provider, subj.Id);
        }
        if (!string.IsNullOrEmpty(subj.ImdbId))
        {
            item.SetProviderId(MetadataProvider.Imdb, subj.ImdbId);
        }
    }

    public static IEnumerable<PersonInfo> BuildPeople(DoubanSubject subj)
    {
        foreach (var d in subj.Directors)
        {
            yield return new PersonInfo { Name = d, Type = PersonKind.Director };
        }
        foreach (var w in subj.Writers)
        {
            yield return new PersonInfo { Name = w, Type = PersonKind.Writer };
        }
        foreach (var c in subj.Cast)
        {
            var p = new PersonInfo { Name = c.Name, Type = PersonKind.Actor };
            if (!string.IsNullOrEmpty(c.Role))
            {
                p.Role = c.Role;
            }
            if (!string.IsNullOrEmpty(c.PhotoUrl))
            {
                p.ImageUrl = c.PhotoUrl;
            }
            yield return p;
        }
    }

    public static RemoteSearchResult ToSearchResult(DoubanSearchEntry e)
    {
        var r = new RemoteSearchResult
        {
            Name = e.Title,
            ImageUrl = e.PosterUrl,
            ProductionYear = e.Year
        };
        r.SetProviderId(DoubanMovieExternalId.Provider, e.Id);
        return r;
    }

    public static IEnumerable<RemoteImageInfo> ToImageInfos(DoubanSubject subj)
    {
        if (!string.IsNullOrEmpty(subj.PosterUrl))
        {
            yield return new RemoteImageInfo
            {
                ProviderName = ProviderName,
                Url = subj.PosterUrl,
                Type = ImageType.Primary
            };
        }
        if (!string.IsNullOrEmpty(subj.BackdropUrl))
        {
            yield return new RemoteImageInfo
            {
                ProviderName = ProviderName,
                Url = subj.BackdropUrl,
                Type = ImageType.Backdrop
            };
        }
    }
}
