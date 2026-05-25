using System;
using System.Collections.Generic;
using Jellyfin.Plugin.CnNfo.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.CnNfo;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public const string PluginGuid = "b9a0f5d2-3e1c-4d5e-8f6a-1b2c3d4e5f60";

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public override Guid Id => new(PluginGuid);

    public override string Name => "CnNfo";

    public override string Description => "默认豆瓣，回退 IMDB / TMDB 的中文元数据插件";

    public static Plugin Instance { get; private set; } = null!;

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
        };
    }
}
