// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
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
