// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Services;

/// <summary>
/// 地理距离计算。
/// </summary>
/// <remarks>
/// 用得上的地方是「按经纬度找最近的气象站」——不少国家级气象服务只按站点发布观测，
/// 不给经纬度查询，那就只能拉回全部站点自己挑最近的那个。
/// </remarks>
public static class GeoDistance
{
    private const double EarthRadiusKm = 6371.0088;

    /// <summary>两点间大圆距离（公里）。</summary>
    public static double HaversineKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        var deltaLatitude = (latitude2 - latitude1) * Math.PI / 180.0;
        var deltaLongitude = (longitude2 - longitude1) * Math.PI / 180.0;
        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                Math.Cos(latitude1 * Math.PI / 180.0) * Math.Cos(latitude2 * Math.PI / 180.0) *
                Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        return EarthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>把字符串解析成坐标，失败返回 false（站点表里常有空值或非数字）。</summary>
    public static bool TryParseCoordinate(string? text, out double value) =>
        double.TryParse(text, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
}
