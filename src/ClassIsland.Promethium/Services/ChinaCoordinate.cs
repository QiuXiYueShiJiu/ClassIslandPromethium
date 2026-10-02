// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Services;

/// <summary>
/// WGS84 / GCJ-02 / BD-09 三套坐标之间的换算。
/// </summary>
/// <remarks>
/// 国内的地图服务不直接用 GPS 的 WGS84 坐标：
/// 高德、腾讯用 GCJ-02（俗称火星坐标），百度在 GCJ-02 基础上再偏一次得到 BD-09。
/// 直接把 WGS84 的点画到 GCJ-02 的底图上，在北京会偏出四五百米——
/// 对一个「精确到街道」的选点器来说，这个偏差不能忍，所以必须换算。
/// <para/>
/// 配置里一律只存 <b>WGS84</b>：换底图不该改变你选的是哪个点，
/// 拿去做天气查询的也得是 WGS84。
/// </remarks>
public static class ChinaCoordinate
{
    private const double A = 6378245.0;
    private const double Ee = 0.00669342162296594323;
    private const double XPi = Math.PI * 3000.0 / 180.0;

    /// <summary>这个点是否在中国大陆范围内（境外不做偏移）。</summary>
    private static bool OutOfChina(double longitude, double latitude) =>
        longitude < 73.66 || longitude > 135.05 || latitude < 3.86 || latitude > 53.55;

    /// <summary>WGS84 转 GCJ-02。</summary>
    public static (double Latitude, double Longitude) Wgs84ToGcj02(double latitude, double longitude)
    {
        if (OutOfChina(longitude, latitude))
        {
            return (latitude, longitude);
        }

        var dLat = TransformLatitude(longitude - 105.0, latitude - 35.0);
        var dLon = TransformLongitude(longitude - 105.0, latitude - 35.0);
        var radLat = latitude / 180.0 * Math.PI;
        var magic = Math.Sin(radLat);
        magic = 1 - Ee * magic * magic;
        var sqrtMagic = Math.Sqrt(magic);

        dLat = dLat * 180.0 / (A * (1 - Ee) / (magic * sqrtMagic) * Math.PI);
        dLon = dLon * 180.0 / (A / sqrtMagic * Math.Cos(radLat) * Math.PI);

        return (latitude + dLat, longitude + dLon);
    }

    /// <summary>
    /// GCJ-02 转 WGS84。
    /// </summary>
    /// <remarks>
    /// 正变换没有代数逆，所以用迭代逼近：拿结果反复回代，
    /// 直到前后两次差得足够小。迭代几次就能收敛到厘米级。
    /// </remarks>
    public static (double Latitude, double Longitude) Gcj02ToWgs84(double latitude, double longitude)
    {
        if (OutOfChina(longitude, latitude))
        {
            return (latitude, longitude);
        }

        var currentLat = latitude;
        var currentLon = longitude;
        for (var i = 0; i < 10; i++)
        {
            var (forwardLat, forwardLon) = Wgs84ToGcj02(currentLat, currentLon);
            var deltaLat = forwardLat - latitude;
            var deltaLon = forwardLon - longitude;
            if (Math.Abs(deltaLat) < 1e-9 && Math.Abs(deltaLon) < 1e-9)
            {
                break;
            }

            currentLat -= deltaLat;
            currentLon -= deltaLon;
        }

        return (currentLat, currentLon);
    }

    /// <summary>GCJ-02 转 BD-09。</summary>
    public static (double Latitude, double Longitude) Gcj02ToBd09(double latitude, double longitude)
    {
        var z = Math.Sqrt(longitude * longitude + latitude * latitude) + 0.00002 * Math.Sin(latitude * XPi);
        var theta = Math.Atan2(latitude, longitude) + 0.000003 * Math.Cos(longitude * XPi);
        return (z * Math.Sin(theta) + 0.006, z * Math.Cos(theta) + 0.0065);
    }

    /// <summary>BD-09 转 GCJ-02。</summary>
    public static (double Latitude, double Longitude) Bd09ToGcj02(double latitude, double longitude)
    {
        var x = longitude - 0.0065;
        var y = latitude - 0.006;
        var z = Math.Sqrt(x * x + y * y) - 0.00002 * Math.Sin(y * XPi);
        var theta = Math.Atan2(y, x) - 0.000003 * Math.Cos(x * XPi);
        return (z * Math.Sin(theta), z * Math.Cos(theta));
    }

    /// <summary>WGS84 转 BD-09。</summary>
    public static (double Latitude, double Longitude) Wgs84ToBd09(double latitude, double longitude)
    {
        var (gcjLat, gcjLon) = Wgs84ToGcj02(latitude, longitude);
        return Gcj02ToBd09(gcjLat, gcjLon);
    }

    /// <summary>BD-09 转 WGS84。</summary>
    public static (double Latitude, double Longitude) Bd09ToWgs84(double latitude, double longitude)
    {
        var (gcjLat, gcjLon) = Bd09ToGcj02(latitude, longitude);
        return Gcj02ToWgs84(gcjLat, gcjLon);
    }

    private static double TransformLatitude(double x, double y)
    {
        var result = -100.0 + 2.0 * x + 3.0 * y + 0.2 * y * y + 0.1 * x * y + 0.2 * Math.Sqrt(Math.Abs(x));
        result += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        result += (20.0 * Math.Sin(y * Math.PI) + 40.0 * Math.Sin(y / 3.0 * Math.PI)) * 2.0 / 3.0;
        result += (160.0 * Math.Sin(y / 12.0 * Math.PI) + 320 * Math.Sin(y * Math.PI / 30.0)) * 2.0 / 3.0;
        return result;
    }

    private static double TransformLongitude(double x, double y)
    {
        var result = 300.0 + x + 2.0 * y + 0.1 * x * x + 0.1 * x * y + 0.1 * Math.Sqrt(Math.Abs(x));
        result += (20.0 * Math.Sin(6.0 * x * Math.PI) + 20.0 * Math.Sin(2.0 * x * Math.PI)) * 2.0 / 3.0;
        result += (20.0 * Math.Sin(x * Math.PI) + 40.0 * Math.Sin(x / 3.0 * Math.PI)) * 2.0 / 3.0;
        result += (150.0 * Math.Sin(x / 12.0 * Math.PI) + 300.0 * Math.Sin(x / 30.0 * Math.PI)) * 2.0 / 3.0;
        return result;
    }
}
