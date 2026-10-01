// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>距离与震感判断，地震相关的计算都收在这里。</summary>
public static class GeoMath
{
    /// <summary>两点间大圆距离（公里）。</summary>
    public static double HaversineKm(double latitude1, double longitude1, double latitude2, double longitude2)
    {
        const double earthRadiusKm = 6371.0088;
        var deltaLatitude = (latitude2 - latitude1) * Math.PI / 180.0;
        var deltaLongitude = (longitude2 - longitude1) * Math.PI / 180.0;
        var a = Math.Sin(deltaLatitude / 2) * Math.Sin(deltaLatitude / 2) +
                Math.Cos(latitude1 * Math.PI / 180.0) * Math.Cos(latitude2 * Math.PI / 180.0) *
                Math.Sin(deltaLongitude / 2) * Math.Sin(deltaLongitude / 2);
        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>
    /// 按震级和震中距粗略判断有没有震感。
    /// </summary>
    /// <remarks>
    /// <b>这是经验规则，不是烈度评定。</b>真正的烈度还要看震源机制、深度、场地条件、
    /// 土层放大等等，光凭震级和距离给不出那个精度。这里只拿它决定「要不要提醒你注意一下」，
    /// 所以条件卡得偏保守。
    /// </remarks>
    public static FeltLikelihood JudgeFelt(double magnitude, double distanceKm)
    {
        if ((magnitude >= 5.0 && distanceKm <= 100) || (magnitude >= 6.0 && distanceKm <= 250))
        {
            return FeltLikelihood.Strong;
        }

        if ((magnitude >= 3.5 && distanceKm <= 60) || (magnitude >= 4.0 && distanceKm <= 150) ||
            (magnitude >= 5.0 && distanceKm <= 300) || (magnitude >= 6.0 && distanceKm <= 600))
        {
            return FeltLikelihood.Slight;
        }

        return FeltLikelihood.Unlikely;
    }
}
