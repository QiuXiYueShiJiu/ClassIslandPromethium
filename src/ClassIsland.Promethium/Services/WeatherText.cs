// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>把归一化后的天气现象换成中文说法，以及风向换算。</summary>
public static class WeatherText
{
    /// <summary>天气现象的中文描述。</summary>
    public static string Describe(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => "晴",
        WeatherCondition.PartlyCloudy => "晴间多云",
        WeatherCondition.Cloudy => "多云",
        WeatherCondition.Overcast => "阴",
        WeatherCondition.Fog => "有雾",
        WeatherCondition.Haze => "有霾",
        WeatherCondition.Drizzle => "毛毛雨",
        WeatherCondition.Rain => "有雨",
        WeatherCondition.HeavyRain => "大雨",
        WeatherCondition.Sleet => "雨夹雪",
        WeatherCondition.Snow => "有雪",
        WeatherCondition.Thunderstorm => "雷阵雨",
        WeatherCondition.Hail => "冰雹",
        _ => "未知"
    };

    /// <summary>罗盘方位缩写转为角度。认不出来时返回 -1（表示未知）。</summary>
    public static double DirectionFromCompass(string? compass)
    {
        if (string.IsNullOrWhiteSpace(compass))
        {
            return -1d;
        }

        switch (compass.Trim().ToUpperInvariant())
        {
            case "N": return 0d;
            case "NNE": return 22.5d;
            case "NE": return 45d;
            case "ENE": return 67.5d;
            case "E": return 90d;
            case "ESE": return 112.5d;
            case "SE": return 135d;
            case "SSE": return 157.5d;
            case "S": return 180d;
            case "SSW": return 202.5d;
            case "SW": return 225d;
            case "WSW": return 247.5d;
            case "W": return 270d;
            case "WNW": return 292.5d;
            case "NW": return 315d;
            case "NNW": return 337.5d;
            default: return -1d;
        }
    }

    /// <summary>
    /// 中文方位词转为角度。认不出来时返回 -1。
    /// </summary>
    /// <remarks>
    /// 心知天气这类接口给的是「东南」「西北风」这种词而不是角度。
    /// 「无持续风向」之类的说法一律返回 -1，界面显示成「风向未知」，不猜一个角度出来。
    /// </remarks>
    public static double DirectionFromChineseCompass(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1d;
        }

        var t = text.Trim().Replace("风", string.Empty);
        switch (t)
        {
            case "北": return 0d;
            case "东北偏北": return 22.5d;
            case "东北": return 45d;
            case "东北偏东": return 67.5d;
            case "东": return 90d;
            case "东南偏东": return 112.5d;
            case "东南": return 135d;
            case "东南偏南": return 157.5d;
            case "南": return 180d;
            case "西南偏南": return 202.5d;
            case "西南": return 225d;
            case "西南偏西": return 247.5d;
            case "西": return 270d;
            case "西北偏西": return 292.5d;
            case "西北": return 315d;
            case "西北偏北": return 337.5d;
            default: return -1d;
        }
    }

    /// <summary>
    /// 蒲福风级换算成公里每小时。
    /// </summary>
    /// <remarks>
    /// 7Timer 只给风级（0~12 的整数），不给风速。这里取每一级的区间中点，
    /// 是有依据的近似而不是随手编的数；但它终究是个近似，界面上要当近似看。
    /// </remarks>
    public static double BeaufortToKmh(int beaufort)
    {
        // 蒲福各级对应的风速区间（km/h）中点
        double[] midpoints =
        {
            0.5, 4.0, 9.0, 16.0, 24.0, 34.0, 45.0, 57.0,
            70.0, 84.0, 98.0, 113.0, 129.0
        };
        if (beaufort < 0) return 0d;
        if (beaufort >= midpoints.Length) return 140d;
        return midpoints[beaufort];
    }

    /// <summary>把风向角度转成中文方位。角度为负表示数据源没给。</summary>
    public static string Direction(double degrees)
    {
        if (degrees < 0)
        {
            return "风向未知";
        }

        string[] names =
        {
            "北", "东北偏北", "东北", "东北偏东", "东", "东南偏东", "东南", "东南偏南",
            "南", "西南偏南", "西南", "西南偏西", "西", "西北偏西", "西北", "西北偏北"
        };
        var index = (int)Math.Round(((degrees % 360) + 360) % 360 / 22.5) % 16;
        return names[index];
    }
}
