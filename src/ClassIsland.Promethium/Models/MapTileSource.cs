// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 地图底图用哪一家。
/// </summary>
/// <remarks>
/// 做成可切换是因为底图这东西**没有全球都好用的**：
/// 国外的服务在国内经常连不上，国内的服务反过来只服务国内。
/// 与其猜用户在哪儿，不如让他自己选，或者交给「自动」按优先级试。
/// </remarks>
public enum MapTileSource
{
    /// <summary>按优先级自动挑：高德 → 百度 → 腾讯 → OpenStreetMap，哪个能用用哪个。</summary>
    Auto = 0,

    /// <summary>高德。标准 XYZ 网格 + GCJ-02 坐标，国内可用性好。</summary>
    AMap = 1,

    /// <summary>百度。自有网格 + BD-09 坐标，编号规律与标准 XYZ 不同。</summary>
    Baidu = 2,

    /// <summary>腾讯。标准 XYZ 网格 + GCJ-02 坐标。</summary>
    Tencent = 3,

    /// <summary>OpenStreetMap。标准 XYZ 网格 + WGS84 坐标，国外可达性好。</summary>
    OpenStreetMap = 4,

    /// <summary>自定义瓦片地址模板。</summary>
    Custom = 5,

    /// <summary>不用底图，只画经纬网格和标记点。</summary>
    None = 6
}

/// <summary>底图瓦片所用的坐标基准。</summary>
public enum TileDatum
{
    /// <summary>GPS 用的 WGS84，OpenStreetMap 就是这套。</summary>
    Wgs84 = 0,

    /// <summary>GCJ-02，高德与腾讯用的火星坐标。</summary>
    Gcj02 = 1,

    /// <summary>BD-09，百度在 GCJ-02 之上再偏一次。</summary>
    Bd09 = 2
}
