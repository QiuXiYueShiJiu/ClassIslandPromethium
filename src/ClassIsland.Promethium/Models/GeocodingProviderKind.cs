// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 地名反查用哪一家。
/// </summary>
/// <remarks>
/// 和底图一样，**没有一家是全球都能用的**：OpenStreetMap 系的 Nominatim
/// 在国内经常连不上，国内的几家又都要密钥。所以做成可切换，默认按顺序自动降级。
/// </remarks>
public enum GeocodingProviderKind
{
    /// <summary>自动：按顺序试，哪个通用哪个。</summary>
    Auto = 0,

    /// <summary>Nominatim（OpenStreetMap）。免密钥，能到街道，但国内常连不上。</summary>
    Nominatim = 1,

    /// <summary>Photon（Komoot）。免密钥，基于 OpenStreetMap，能到街道和门牌。</summary>
    Photon = 2,

    /// <summary>BigDataCloud。免密钥，给行政区划层级（能到街道），响应快。</summary>
    BigDataCloud = 3,

    /// <summary>高德。国内最好用，但需要自己去申请免费密钥。</summary>
    AMap = 4
}
