// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.IO;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 天气图标的三套画法：字形、emoji、自定义图片。
/// </summary>
/// <remarks>
/// 字形码位取自 FluentAvalonia 的 Symbol 枚举里那组 Weather* 成员，
/// 每个码位都能对回来源，不是抄来的魔数。
/// </remarks>
public static class WeatherIconCatalog
{
    /// <summary>取系统图标字形。</summary>
    public static string Glyph(WeatherCondition condition, bool isDay) => condition switch
    {
        WeatherCondition.Clear => isDay ? "\uF811A" : "\uF8102",
        WeatherCondition.PartlyCloudy => isDay ? "\uF8104" : "\uF8106",
        WeatherCondition.Cloudy => "\uF80F4",
        WeatherCondition.Overcast => "\uF80F4",
        WeatherCondition.Fog => "\uF80FA",
        WeatherCondition.Haze => "\uF8100",
        WeatherCondition.Drizzle => "\uF80F6",
        WeatherCondition.Rain => "\uF8108",
        WeatherCondition.HeavyRain => "\uF8108",
        WeatherCondition.Sleet => "\uF810E",
        WeatherCondition.Snow => "\uF8110",
        WeatherCondition.Thunderstorm => "\uF8120",
        WeatherCondition.Hail => isDay ? "\uF80FC" : "\uF80FE",
        _ => "\uF80F4"
    };

    /// <summary>取 emoji。</summary>
    public static string Emoji(WeatherCondition condition, bool isDay) => condition switch
    {
        WeatherCondition.Clear => isDay ? "☀️" : "🌙",
        WeatherCondition.PartlyCloudy => isDay ? "⛅" : "☁️",
        WeatherCondition.Cloudy => "☁️",
        WeatherCondition.Overcast => "🌥️",
        WeatherCondition.Fog => "🌫️",
        WeatherCondition.Haze => "😶‍🌫️",
        WeatherCondition.Drizzle => "🌦️",
        WeatherCondition.Rain => "🌧️",
        WeatherCondition.HeavyRain => "🌧️",
        WeatherCondition.Sleet => "🌨️",
        WeatherCondition.Snow => "❄️",
        WeatherCondition.Thunderstorm => "⛈️",
        WeatherCondition.Hail => "🧊",
        _ => "❓"
    };

    /// <summary>自定义图片模式下，这个天气现象对应的文件名主干。</summary>
    public static string FileKey(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => "clear",
        WeatherCondition.PartlyCloudy => "partlycloudy",
        WeatherCondition.Cloudy => "cloudy",
        WeatherCondition.Overcast => "overcast",
        WeatherCondition.Fog => "fog",
        WeatherCondition.Haze => "haze",
        WeatherCondition.Drizzle => "drizzle",
        WeatherCondition.Rain => "rain",
        WeatherCondition.HeavyRain => "heavyrain",
        WeatherCondition.Sleet => "sleet",
        WeatherCondition.Snow => "snow",
        WeatherCondition.Thunderstorm => "thunderstorm",
        WeatherCondition.Hail => "hail",
        _ => "unknown"
    };

    /// <summary>自定义图片模式支持的扩展名。</summary>
    public static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif" };

    /// <summary>
    /// 在给定目录里找这个天气现象对应的图片。
    /// </summary>
    /// <remarks>
    /// 命名约定：晴天叫 <c>clear.png</c>，夜间版本叫 <c>clear_night.png</c>。
    /// 夜间优先找 _night，找不到就退回白天的图，这样用户只准备一套图也能用。
    /// </remarks>
    public static string? ResolveImagePath(string folder, WeatherCondition condition, bool isDay)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return null;
        }

        var key = FileKey(condition);
        var names = isDay
            ? new[] { key + "_day", key }
            : new[] { key + "_night", key };

        foreach (var name in names)
        {
            foreach (var extension in SupportedExtensions)
            {
                var path = Path.Combine(folder, name + extension);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    /// <summary>列出所有约定的文件名，放在设置页里提示用户该怎么命名。</summary>
    public static string DescribeNamingConvention()
    {
        var keys = new[]
        {
            WeatherCondition.Clear, WeatherCondition.PartlyCloudy, WeatherCondition.Cloudy,
            WeatherCondition.Overcast, WeatherCondition.Fog, WeatherCondition.Haze, WeatherCondition.Drizzle,
            WeatherCondition.Rain, WeatherCondition.HeavyRain, WeatherCondition.Sleet,
            WeatherCondition.Snow, WeatherCondition.Thunderstorm, WeatherCondition.Hail
        };
        return string.Join("、", keys.Select(k => FileKey(k) + ".png"));
    }
}
