# 发布指南

## 目录

1. 本机编译 & 自验证
2. GitHub Releases 自动发布
3. 国内镜像源
4. Jellyfin 插件仓库（manifest.json）
5. 介绍图 / 商店封面
6. FAQ：`NotSupported`、404、风控等

---

## 1. 本机编译 & 自验证

```bash
git clone https://github.com/BryanXBY/Jellyfin.Plugin.CnNfo.git
cd Jellyfin.Plugin.CnNfo
dotnet build src/Jellyfin.Plugin.CnNfo -c Release
```

产物：

```
src/Jellyfin.Plugin.CnNfo/bin/Release/net9.0/Jellyfin.Plugin.CnNfo.dll
```

### 1.1 安装到原生 Jellyfin（Windows / Linux）

- Windows：复制 dll 到 `%ProgramData%\Jellyfin\Server\plugins\CnNfo\`，重启 Jellyfin 服务
- Linux：`/var/lib/jellyfin/plugins/CnNfo/`，`systemctl restart jellyfin`

### 1.2 安装到 Docker 中的 Jellyfin

**先找 `/config` 挂在 host 上哪里：**

```bash
docker inspect jellyfin --format '{{ range .Mounts }}{{ .Destination }} -> {{ .Source }}{{ "\n" }}{{ end }}'
```

记下 `/config` 对应的 host 路径，下文记为 `<HOST_CONFIG>`。

**方法 A：直接拷到 host volume**

```powershell
# Windows
$dest = "<HOST_CONFIG>\plugins\CnNfo"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item src\Jellyfin.Plugin.CnNfo\bin\Release\net9.0\Jellyfin.Plugin.CnNfo.dll $dest
docker restart jellyfin
```

```bash
# Linux / macOS
sudo mkdir -p <HOST_CONFIG>/plugins/CnNfo
sudo cp src/Jellyfin.Plugin.CnNfo/bin/Release/net9.0/Jellyfin.Plugin.CnNfo.dll <HOST_CONFIG>/plugins/CnNfo/
docker restart jellyfin
```

**方法 B：docker cp（不用知道 host 路径）**

```bash
docker exec jellyfin mkdir -p /config/plugins/CnNfo
docker cp src/Jellyfin.Plugin.CnNfo/bin/Release/net9.0/Jellyfin.Plugin.CnNfo.dll jellyfin:/config/plugins/CnNfo/
docker restart jellyfin
```

**验证：**

```bash
docker exec jellyfin ls -la /config/plugins/CnNfo
docker logs --tail 100 jellyfin | grep -i cnnfo
```

打开 Jellyfin → 控制台 → 插件，应看到 **CnNfo** 处于 **Active**。

> 注意 `linuxserver/jellyfin` 容器内运行用户是 `abc:abc (1000:1000)`，从 host 直接 cp 后可能要 `chown -R 1000:1000 <HOST_CONFIG>/plugins/CnNfo`；官方 `jellyfin/jellyfin` 镜像默认用 root，没有这个问题。

### 1.3 通过插件仓库一键装（最优雅，对 Docker 用户最友好）

不用 cp、不用重启容器、不用 build：用户只要在 Jellyfin UI 里加上你的 manifest URL，就能下载安装升级，参见第 4 节。

> 验证：打开 Jellyfin → 控制台 → 插件，应看到 **CnNfo** 处于 **Active**。如果状态是 `NotSupported`，请见第 6 节 FAQ。

---

## 2. GitHub Releases 自动发布

发布流程已在 `.github/workflows/release.yml` 自动化。打 tag 即可：

```bash
git tag v1.0.0
git push origin v1.0.0
```

`release.yml` 会：

1. 用 .NET 9 SDK 构建 Release 配置
2. 把 dll 打成 `cnnfo-1.0.0.zip`，算 md5
3. 调用 `.github/scripts/update-manifest.py` 把这一版插入 `manifest.json` 顶部
4. 把改好的 `manifest.json` 提交回 main
5. 把 zip 上传到 GitHub Release

之后用户只要在 Jellyfin 里添加你的 manifest URL，新版本就会自动出现。

---

## 3. 国内镜像源

GitHub 在大陆下载体验差。推荐三种镜像：

| 镜像 | 用法 |
|---|---|
| **gh-proxy** | `https://gh-proxy.com/raw.githubusercontent.com/BryanXBY/Jellyfin.Plugin.CnNfo/main/manifest.json` |
| **ghfast.top** | `https://ghfast.top/raw.githubusercontent.com/BryanXBY/Jellyfin.Plugin.CnNfo/main/manifest.json` |
| **个人镜像（建议）** | 把 `manifest.json` 复制到 Gitee 或对象存储（七牛/COS/OSS）后引用 |

为了让 Jellyfin 客户端下载 zip 也走加速，可以在发布脚本里把 `sourceUrl` 同步改成镜像 URL。例如把 `update-manifest.py` 里的 URL 模板改为：

```python
mirror_url = args.url.replace("github.com", "gh-proxy.com/github.com")
```

或者在仓库里维护两份 manifest：`manifest.json`（原始 github）+ `manifest-cn.json`（镜像）。

---

## 4. Jellyfin 插件仓库

`manifest.json` 是 Jellyfin 插件仓库的索引文件。结构：

```json
[
  {
    "guid": "b9a0f5d2-3e1c-4d5e-8f6a-1b2c3d4e5f60",
    "name": "CnNfo",
    "description": "...",
    "overview": "...",
    "owner": "BryanXBY",
    "category": "Metadata",
    "imageUrl": ".../assets/banner.png",
    "versions": [
      {
        "version": "1.0.0.0",
        "changelog": "...",
        "targetAbi": "10.11.0.0",
        "sourceUrl": ".../cnnfo-1.0.0.zip",
        "checksum": "<md5>",
        "timestamp": "2026-05-25T12:34:56Z"
      }
    ]
  }
]
```

用户安装方式：**控制台 → 插件 → 仓库 → 添加** → URL 填上面那一条 manifest.json 即可。

---

## 5. 介绍图 / 商店封面

仓库自带 `assets/banner.svg`（矢量源）。Jellyfin 商店封面要求 PNG，推荐 **600×300** 或 **800×400**：

### Windows 用 ImageMagick（无需 Docker）

```powershell
choco install imagemagick
magick assets/banner.svg -resize 800x400 -background white -flatten assets/banner.png
```

### Windows 用 Inkscape

```powershell
choco install inkscape
inkscape assets/banner.svg --export-type=png --export-filename=assets/banner.png --export-width=800
```

### 在线工具

* <https://cloudconvert.com/svg-to-png>
* <https://convertio.co/svg-png/>

转好的 `banner.png` 提交到 `assets/` 即可被 `manifest.json` 的 `imageUrl` 引用。

---

## 6. FAQ

### Q. 插件状态显示 `NotSupported`

99% 是 ABI 不匹配。`build.yaml` 里的 `targetAbi` 必须 ≤ Jellyfin 服务器版本，且主次版本号一致。

- Jellyfin 10.10.x ↔ `targetAbi: 10.10.0.0` + .NET 8
- Jellyfin 10.11.x ↔ `targetAbi: 10.11.0.0` + .NET 9（本插件默认）

如果你在 10.10 服务器上看到本插件 NotSupported，把 csproj 里的 `Jellyfin.Controller` 改为 `10.10.x`，把 `build.yaml` 的 `targetAbi` 改为 `10.10.0.0`、`framework` 改为 `net8.0` 重新编译。

### Q. 豆瓣返回 403 / "异常请求"

- 在配置页填入登录后的 Cookie（至少要有 `bid=` 和 `dbcl2=`）。
- 把 `RequestIntervalMs` 调到 2000 或更高。
- 检查 IP 是否在公共代理/机房网段——豆瓣对这些段位风控特别严。

### Q. TMDB 总是空？

- 在 themoviedb.org 注册并申请 API v3 key
- 在配置页填好 `TmdbApiKey`
- `PreferredLanguage` 留 `zh-CN`，TMDB 会优先返回中文标题/简介

### Q. 我想关掉 IMDB 兜底（避免要 OMDb key）

配置页取消勾选 "豆瓣命中失败时回退 IMDB / OMDb" 即可，OMDb API Key 留空也不会请求。
