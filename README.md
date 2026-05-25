# Jellyfin.Plugin.CnNfo

面向中文用户的 Jellyfin 元数据插件。默认从 **豆瓣** 抓取，无法命中时回退 **IMDB**，再回退 **TMDB**。支持电影 / 电视剧 / 动画，内置文件名识别（AnitomySharp）、智能译名拆分、本地缓存与频控保护、按"电影/电视剧"分类对搜索结果排序。

> Author: **BryanXBY** · <https://github.com/BryanXBY>

> 目标平台：**Jellyfin 10.11.x (.NET 9)**。低于 10.11 的版本请使用 [MetaShark](https://github.com/cxfksword/jellyfin-plugin-metashark) 或 [JellyfinPluginDouban](https://github.com/Xzonn/JellyfinPluginDouban) 的对应 ABI。

## 功能

- 默认数据源：**豆瓣**（电影 / 电视剧 / 动画）
- 三级 Fallback：**豆瓣 → IMDB → TMDB**
- 豆瓣账号 Cookie 配置，规避匿名请求被风控
- 文件名解析使用 **AnitomySharp**（动画番组命名、字幕组、季/集）
- 译名拆分：处理"原名 译名"中含空格的歧义，借助字符语言检测推断分界
- 默认语言：**简体中文**（zh-CN），影片标题、人物姓名优先中文
- 内置 **MemoryCache** + 速率限制，避免触发豆瓣风控
- 搜索结果按类型分类排序（指定 Series 时把"电视剧"条目排到前面）
- 纯插件 DLL，**不需要任何 Docker / 外部服务**

## 安装

### 方式 1：从插件仓库（推荐）

在 Jellyfin → 控制台 → 插件 → 仓库 添加：

```
https://raw.githubusercontent.com/BryanXBY/Jellyfin.Plugin.CnNfo/main/manifest.json
```

国内镜像：

```
https://gh-proxy.com/raw.githubusercontent.com/BryanXBY/Jellyfin.Plugin.CnNfo/main/manifest.json
```

然后在 目录 中安装 **CnNfo**，重启 Jellyfin。

### 方式 2：手动安装

1. 从 [Releases](../../releases) 下载最新 zip
2. 解压出 `Jellyfin.Plugin.CnNfo.dll`
3. 放入 `<JellyfinData>/plugins/CnNfo/`（无此目录请新建）
4. 重启 Jellyfin

## 配置

控制台 → 插件 → CnNfo：

| 配置项 | 说明 |
|---|---|
| `DoubanCookie` | 豆瓣 Cookie 串（浏览器 F12 复制 `bid=...; dbcl2=...`）。留空走匿名访问 |
| `TmdbApiKey` | TMDB API Key（兜底用）。建议申请：<https://www.themoviedb.org/settings/api> |
| `EnableImdbFallback` | 关掉则只走 豆瓣 → TMDB |
| `CacheMinutes` | 元数据缓存时长，默认 60 分钟 |
| `RequestIntervalMs` | 两次豆瓣请求最小间隔毫秒数，默认 1500 |
| `PreferOriginalTitle` | 关闭则使用译名作为主标题（默认关，中文优先）|

## 编译

```bash
git clone <repo>
cd JellyfinCnNfo
dotnet build src/Jellyfin.Plugin.CnNfo -c Release
```

产物：`src/Jellyfin.Plugin.CnNfo/bin/Release/net9.0/Jellyfin.Plugin.CnNfo.dll`

## 鸣谢

- [MetaShark](https://github.com/cxfksword/jellyfin-plugin-metashark)
- [JellyfinPluginDouban](https://github.com/Xzonn/JellyfinPluginDouban)
- [OpenDouban](https://github.com/caryyu/jellyfin-plugin-opendouban)
- [AnitomySharp](https://github.com/chu-shen/AnitomySharp)

## License

GPL-3.0
