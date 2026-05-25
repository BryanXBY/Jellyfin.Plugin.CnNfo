namespace Jellyfin.Plugin.CnNfo.Api;

public class DoubanCookieJar
{
    public string Current
    {
        get
        {
            var cfg = Plugin.Instance?.Configuration;
            return cfg?.DoubanCookie ?? string.Empty;
        }
    }

    public bool HasCookie => !string.IsNullOrWhiteSpace(Current);
}
