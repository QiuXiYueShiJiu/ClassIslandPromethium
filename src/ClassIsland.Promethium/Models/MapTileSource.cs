// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 地图底图用哪一家。
/// </summary>
/// <remarks>
/// 做成可切换是因为底图这东西**没有全球都好用的**：
/// OpenStreetMap 的瓦片服务器在国内经常连不上，天地图反过来只服务国内、
/// 境外 IP 会被 WAF 直接拦掉。与其猜用户在哪儿，不如让他自己选。
/// </remarks>
public enum MapTileSource
{
    /// <summary>OpenStreetMap，默认。国外可达性好，国内经常连不上。</summary>
    OpenStreetMap = 0,

    /// <summary>天地图。国内官方服务，需要一个免费密钥。</summary>
    Tianditu = 1,

    /// <summary>自定义瓦片地址模板。</summary>
    Custom = 2,

    /// <summary>不用底图，只画经纬网格和标记点。</summary>
    None = 3
}
