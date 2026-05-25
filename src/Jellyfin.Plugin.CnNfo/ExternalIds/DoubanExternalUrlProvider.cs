using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.CnNfo.ExternalIds;

/// <summary>
/// 10.11 把 URL 模板从 IExternalId 移到 IExternalUrlProvider。
/// 这里负责把存到 BaseItem 上的 Douban 条目 ID 渲染成 douban.com 链接。
/// </summary>
public class DoubanExternalUrlProvider : IExternalUrlProvider
{
    public string Name => DoubanMovieExternalId.Provider;

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item.ProviderIds.TryGetValue(DoubanMovieExternalId.Provider, out var id)
            && !string.IsNullOrEmpty(id))
        {
            yield return $"https://movie.douban.com/subject/{id}/";
        }
    }
}
