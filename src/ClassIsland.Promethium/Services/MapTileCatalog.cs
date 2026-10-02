// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>一套底图的取图规则。</summary>
/// <param name="UrlTemplate">地址模板，支持 {s} {z} {x} {y} {key} 占位符；空串表示不用底图。</param>
/// <param name="Subdomains">{s} 的取值字符集，用于分散请求；空串表示没有 {s}。</param>
/// <param name="Attribution">必须显示的版权署名。</param>
public record MapTileSpec(string UrlTemplate, string Subdomains, string Attribution)
{
    /// <summary>是否真的会去取瓦片。</summary>
    public bool HasBasemap => !string.IsNullOrWhiteSpace(UrlTemplate);
}

/// <summary>
/// 底图台账：把配置解析成取图规则，并按规则拼出具体地址。
/// </summary>
/// <remarks>
/// 拼地址这件事单独拎出来，是为了能脱离界面直接测——
/// 地址拼错的话地图就是一片空白，而空白是很难从现象上定位到原因的。
/// </remarks>
public static class MapTileCatalog
{
    private const string OpenStreetMapTemplate = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>
    /// 天地图的 WMTS 地址。
    /// </summary>
    /// <remarks>
    /// 天地图要求带 <c>tk</c>，也就是用户自己去官网申请的免费密钥。
    /// 境内可用性很好，境外 IP 会被它的 WAF 拦掉，所以不适合当默认值。
    /// </remarks>
    private const string TiandituTemplate =
        "https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0" +
        "&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles" +
        "&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk={key}";

    /// <summary>按配置解析出取图规则。</summary>
    public static MapTileSpec Resolve(WeatherConfig config) => config.MapTileSource switch
    {
        MapTileSource.None => new MapTileSpec(string.Empty, string.Empty, "未使用底图"),
        MapTileSource.Tianditu => new MapTileSpec(TiandituTemplate, "01234567", "© 天地图"),
        MapTileSource.Custom => new MapTileSpec(
            config.CustomTileUrl,
            "0123",
            string.IsNullOrWhiteSpace(config.CustomTileAttribution) ? "自定义底图" : config.CustomTileAttribution),
        _ => new MapTileSpec(OpenStreetMapTemplate, string.Empty, "© OpenStreetMap 贡献者")
    };

    /// <summary>
    /// 按规则拼出一张瓦片的地址。规则为空时返回 null。
    /// </summary>
    /// <param name="spec">取图规则。</param>
    /// <param name="apiKey">天地图这类需要密钥的底图用它。</param>
    /// <param name="zoom">缩放级别。</param>
    /// <param name="x">瓦片列号。</param>
    /// <param name="y">瓦片行号。</param>
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
