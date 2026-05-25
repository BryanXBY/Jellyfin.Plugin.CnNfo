using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.CnNfo.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.CnNfo.Api;

/// <summary>
/// 反向代理豆瓣图床，给 Jellyfin Web UI 用。
/// 浏览器直接拉 img1.doubanio.com 会被防盗链 403——所以让浏览器拉本插件的
/// /Plugins/CnNfo/Image?url=...，我们再带上正确 Referer 去拉。
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("Plugins/CnNfo")]
public class CnNfoApiController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CnNfoApiController> _logger;

    public CnNfoApiController(IHttpClientFactory httpClientFactory, ILogger<CnNfoApiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpGet("Image")]
    [Produces("image/jpeg", "image/png", "image/webp", "image/gif")]
    public async Task<IActionResult> ProxyImage([FromQuery] string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(url))
        {
            return BadRequest("missing url");
        }

        // 仅允许豆瓣域名，避免被滥用做开放代理
        if (!url.StartsWith("https://", StringComparison.Ordinal)
            && !url.StartsWith("http://", StringComparison.Ordinal))
        {
            return BadRequest("only http(s) allowed");
        }

        Uri target;
        try
        {
            target = new Uri(url);
        }
        catch (UriFormatException)
        {
            return BadRequest("invalid url");
        }

        var host = target.Host.ToLowerInvariant();
        if (!host.EndsWith("doubanio.com", StringComparison.Ordinal)
            && !host.EndsWith("douban.com", StringComparison.Ordinal))
        {
            return BadRequest("only doubanio.com / douban.com hosts allowed");
        }

        var client = _httpClientFactory.CreateClient("CnNfo.Image");
        client.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Get, target);
        req.Headers.UserAgent.ParseAdd(HttpUtil.DesktopUserAgent);
        req.Headers.Referrer = new Uri("https://movie.douban.com/");

        try
        {
            var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("CnNfo image proxy upstream HTTP {Status} for {Url}", (int)resp.StatusCode, url);
                return StatusCode((int)resp.StatusCode);
            }
            var contentType = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            // 让浏览器缓存 1 天，少拖累豆瓣
            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CnNfo image proxy exception for {Url}", url);
            return StatusCode(502);
        }
    }
}
