// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>一套底图的取图规则。</summary>
/// <param name="Name">界面上显示的名字。</param>
/// <param name="UrlTemplate">地址模板，支持 {s} {z} {x} {y} {key}；空串表示不用底图。</param>
/// <param name="Subdomains">{s} 的取值字符集，空串表示没有 {s}。</param>
/// <param name="Attribution">必须显示的版权署名。</param>
/// <param name="Datum">这套瓦片用的坐标基准。</param>
public record MapTileSpec(
    string Name,
    string UrlTemplate,
    string Subdomains,
    string Attribution,
    TileDatum Datum)
{
    /// <summary>是否真的会去取瓦片。</summary>
    public bool HasBasemap => !string.IsNullOrWhiteSpace(UrlTemplate);

    /// <summary>不用底图的占位规则。</summary>
    public static MapTileSpec None { get; } = new("不用底图", string.Empty, string.Empty, "未使用底图", TileDatum.Wgs84);
}

/// <summary>
/// 底图台账：把配置解析成一串候选规则，并按规则拼出具体地址。
/// </summary>
/// <remarks>
/// 拼地址这件事单独拎出来，是为了能脱离界面直接测——
/// 地址拼错的话地图就是一片空白，而空白很难从现象上定位到原因。
/// </remarks>
public static class MapTileCatalog
{
    /// <summary>高德。标准 XYZ 网格，内容按 GCJ-02 绘制。</summary>
    private const string AMapTemplate =
        "https://webrd0{s}.is.autonavi.com/appmaptile?lang=zh_cn&size=1&scale=1&style=8&x={x}&y={y}&z={z}";

    /// <summary>
    /// 百度。
    /// </summary>
    /// <remarks>
    /// <b>这条未经验证。</b>百度的瓦片编号不是标准 XYZ（它以自己的网格原点编号，
    /// 而且坐标基准是 BD-09）。开发环境按这个地址取回来的是空白占位图，
    /// 所以既没能确认编号规律，也没能目视核对对齐。列在这里是为了尊重使用者的选择，
    /// 真要用建议拿到可用的地址走「自定义」。
    /// </remarks>
    private const string BaiduTemplate =
        "https://maponline{s}.bdimg.com/tile/?qt=vtile&x={x}&y={y}&z={z}&styles=pl&scaler=1&udt=20200101";

    /// <summary>腾讯。标准 XYZ 网格 + GCJ-02。</summary>
    private const string TencentTemplate =
        "https://rt{s}.map.gtimg.com/tile?z={z}&x={x}&y={y}&styleid=3&version=297";

    /// <summary>OpenStreetMap。标准 XYZ + WGS84。</summary>
    private const string OpenStreetMapTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    private static MapTileSpec AMap => new("高德", AMapTemplate, "1234", "© 高德地图", TileDatum.Gcj02);
    private static MapTileSpec Baidu => new("百度", BaiduTemplate, "0123", "© 百度地图", TileDatum.Bd09);
    private static MapTileSpec Tencent => new("腾讯", TencentTemplate, "0123", "© 腾讯地图", TileDatum.Gcj02);
    private static MapTileSpec OpenStreetMap => new("OpenStreetMap", OpenStreetMapTemplate, string.Empty, "© OpenStreetMap 贡献者", TileDatum.Wgs84);

    /// <summary>
    /// 按配置解析出一串候选规则，界面按顺序试，前一个不行就用下一个。
    /// </summary>
    /// <remarks>
    /// 自动模式的优先级是<b>高德 → 百度 → 腾讯 → OpenStreetMap</b>。
    /// 之所以要成一串而不是一个，是因为这几家没有一家能保证到处都能用：
    /// 国内服务到国外多半连不上，国外服务在国内也常常不通。
    /// </remarks>
    public static IReadOnlyList<MapTileSpec> ResolveChain(WeatherConfig config) => config.MapTileSource switch
    {
        MapTileSource.None => new[] { MapTileSpec.None },
        MapTileSource.AMap => new[] { AMap },
        MapTileSource.Baidu => new[] { Baidu },
        MapTileSource.Tencent => new[] { Tencent },
        MapTileSource.OpenStreetMap => new[] { OpenStreetMap },
        MapTileSource.Custom => new[]
        {
            new MapTileSpec("自定义",
                config.CustomTileUrl,
                "0123",
                string.IsNullOrWhiteSpace(config.CustomTileAttribution) ? "自定义底图" : config.CustomTileAttribution,
                config.CustomTileDatum)
        },
        // 自动：按优先级排，最后一个用 OpenStreetMap 兜底
        _ => new[] { AMap, Baidu, Tencent, OpenStreetMap }
    };

    /// <summary>按规则拼出一张瓦片的地址。规则为空时返回 null。</summary>
    public static string? BuildUrl(MapTileSpec spec, string apiKey, int zoom, int x, int y)
    {
        if (!spec.HasBasemap)
        {
            return null;
        }

        var url = spec.UrlTemplate;
        if (url.Contains("{s}", StringComparison.Ordinal))
        {
            // 用行列和取模挑子域：同一张图每次挑到同一个子域，便于命中缓存
            var subdomains = spec.Subdomains.Length > 0 ? spec.Subdomains : "0";
            var index = ((x + y) % subdomains.Length + subdomains.Length) % subdomains.Length;
            url = url.Replace("{s}", subdomains[index].ToString(), StringComparison.Ordinal);
        }

        return url
            .Replace("{z}", zoom.ToString(), StringComparison.Ordinal)
            .Replace("{x}", x.ToString(), StringComparison.Ordinal)
            .Replace("{y}", y.ToString(), StringComparison.Ordinal)
            .Replace("{key}", apiKey ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>检查自定义模板是否可用，附上人能看懂的原因。</summary>
    public static bool ValidateCustomTemplate(string template, out string reason)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            reason = "自定义底图地址是空的";
            return false;
        }

        if (!Uri.TryCreate(template.Replace("{s}", "0").Replace("{z}", "1")
                .Replace("{x}", "0").Replace("{y}", "0").Replace("{key}", "k"), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            reason = "地址不像一个 http/https 链接";
            return false;
        }

        if (!template.Contains("{z}", StringComparison.Ordinal) ||
            !template.Contains("{x}", StringComparison.Ordinal) ||
            !template.Contains("{y}", StringComparison.Ordinal))
        {
            reason = "地址里必须同时含 {z}、{x}、{y} 三个占位符";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
