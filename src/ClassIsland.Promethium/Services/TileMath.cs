// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// Web 墨卡托瓦片坐标换算。
/// </summary>
/// <remarks>
/// 抽出来是因为两处都要用：地图控件要按它画图，
/// 「自动配置」要按它算出该去试哪一张瓦片来判断这个底图通不通。
/// 各写一份的话，两边一旦不一致就会出现「探测说通、画出来却错位」这种怪事。
/// </remarks>
public static class TileMath
{
    /// <summary>一张瓦片的边长（像素）。</summary>
    public const int TileSize = 256;

    /// <summary>经度转世界像素横坐标。</summary>
    public static double LongitudeToWorldX(double longitude, int scale) =>
        (longitude + 180.0) / 360.0 * TileSize * scale;

    /// <summary>纬度转世界像素纵坐标。</summary>
    public static double LatitudeToWorldY(double latitude, int scale)
    {
        var clamped = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var radians = clamped * Math.PI / 180.0;
        return (0.5 - Math.Log((1 + Math.Sin(radians)) / (1 - Math.Sin(radians))) / (4 * Math.PI))
               * TileSize * scale;
    }

    /// <summary>世界像素横坐标转经度。</summary>
    public static double WorldXToLongitude(double x, int scale) =>
        x / (TileSize * (double)scale) * 360.0 - 180.0;

    /// <summary>世界像素纵坐标转纬度。</summary>
    public static double WorldYToLatitude(double y, int scale)
    {
        var n = Math.PI - 2.0 * Math.PI * y / (TileSize * (double)scale);
        return 180.0 / Math.PI * Math.Atan(Math.Sinh(n));
    }

    /// <summary>
    /// 算出某个 WGS84 坐标在指定底图基准下落进哪一张瓦片。
    /// </summary>
    /// <param name="latitude">纬度（WGS84）。</param>
    /// <param name="longitude">经度（WGS84）。</param>
    /// <param name="zoom">缩放级别。</param>
    /// <param name="datum">底图用的坐标基准。</param>
    public static (int X, int Y) TileOf(double latitude, double longitude, int zoom, TileDatum datum)
    {
        var (convertedLatitude, convertedLongitude) = datum switch
        {
            TileDatum.Gcj02 => ChinaCoordinate.Wgs84ToGcj02(latitude, longitude),
            TileDatum.Bd09 => ChinaCoordinate.Wgs84ToBd09(latitude, longitude),
            _ => (latitude, longitude)
        };

        var scale = 1 << zoom;
        var x = (int)Math.Floor(LongitudeToWorldX(convertedLongitude, scale) / TileSize);
        var y = (int)Math.Floor(LatitudeToWorldY(convertedLatitude, scale) / TileSize);
        return (x, y);
    }
}
