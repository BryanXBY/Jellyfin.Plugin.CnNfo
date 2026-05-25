using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Util;

internal static class HttpUtil
{
    public const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/124.0.0.0 Safari/537.36";

    public static HttpRequestMessage NewGet(string url, string? referer = null, string? cookie = null)
    {
        var msg = new HttpRequestMessage(HttpMethod.Get, url);
        msg.Headers.UserAgent.ParseAdd(DesktopUserAgent);
        msg.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        msg.Headers.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
        if (!string.IsNullOrWhiteSpace(referer))
        {
            msg.Headers.Referrer = new Uri(referer);
        }
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            msg.Headers.TryAddWithoutValidation("Cookie", cookie);
        }
        return msg;
    }

    public static async Task<string?> SendWithRetryAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        ILogger logger,
        CancellationToken ct,
        int maxAttempts = 3)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var request = requestFactory();
            HttpResponseMessage? response = null;
            try
            {
                response = await client.SendAsync(request, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    logger.LogWarning("CnNfo HTTP {Status} on {Url}, retrying in {Delay}s",
                        (int)response.StatusCode, request.RequestUri, delay.TotalSeconds);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("CnNfo HTTP {Status} on {Url}",
                        (int)response.StatusCode, request.RequestUri);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "CnNfo HTTP exception, retrying ({Attempt})", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct).ConfigureAwait(false);
            }
            finally
            {
                response?.Dispose();
            }
        }
        return null;
    }
}
