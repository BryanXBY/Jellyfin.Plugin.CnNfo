using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.CnNfo.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>豆瓣 cookie 串：bid=...; dbcl2=...; ck=...</summary>
    public string DoubanCookie { get; set; } = string.Empty;

    /// <summary>TMDB v3 API key（兜底用）。留空则跳过 TMDB。</summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>OMDb / IMDb 中间层 API Key（兜底用，可选）。</summary>
    public string OmdbApiKey { get; set; } = string.Empty;

    public bool EnableImdbFallback { get; set; } = true;

    public bool EnableTmdbFallback { get; set; } = true;

    /// <summary>元数据缓存时长，单位分钟。</summary>
    public int CacheMinutes { get; set; } = 60;

    /// <summary>两次豆瓣请求最小间隔毫秒。</summary>
    public int RequestIntervalMs { get; set; } = 1500;

    /// <summary>当为 false 时把中文译名作为主标题（默认）。</summary>
    public bool PreferOriginalTitle { get; set; } = false;

    public string PreferredLanguage { get; set; } = "zh-CN";

    public string PreferredCountry { get; set; } = "CN";

    public bool DebugLog { get; set; } = false;
}
