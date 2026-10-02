// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 按天气描述文字反推天气现象。
/// </summary>
/// <remarks>
/// 各家数据源的数字代码体系互不兼容，但描述文字反而高度一致——
/// 尤其国内那几家，返回的就是「晴」「多云」「雷阵雨」这类中文词。
/// 所以对它们统一走文字映射，比挨个去啃各家的码表可靠得多。
/// <para/>
/// 判断顺序有讲究：先掐更具体的词。比如「雨夹雪」同时含「雨」和「雪」，
/// 「雷阵雨」同时含「雷」和「雨」，顺序错了就会归类到错的那一类。
/// </remarks>
public static class WeatherConditionText
{
    /// <summary>按中文描述判断。</summary>
    public static WeatherCondition FromChinese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return WeatherCondition.Unknown;
        }

        if (text.Contains("雷")) return WeatherCondition.Thunderstorm;
        if (text.Contains("冰雹")) return WeatherCondition.Hail;
        if (text.Contains("雨夹雪") || text.Contains("冻雨") || text.Contains("冻毛毛雨")) return WeatherCondition.Sleet;
        if (text.Contains("雪")) return WeatherCondition.Snow;
        if (text.Contains("雾")) return WeatherCondition.Fog;
        if (text.Contains("霾") || text.Contains("浮尘") || text.Contains("扬沙") || text.Contains("沙尘")) return WeatherCondition.Haze;
        if (text.Contains("暴雨") || text.Contains("大雨")) return WeatherCondition.HeavyRain;
        if (text.Contains("毛毛雨") || text.Contains("细雨") || text.Contains("小雨")) return WeatherCondition.Rain;
        if (text.Contains("雨")) return WeatherCondition.Rain;
        if (text.Contains("阴")) return WeatherCondition.Overcast;
        if (text.Contains("多云") || text.Contains("少云")) return WeatherCondition.PartlyCloudy;
        if (text.Contains("晴")) return WeatherCondition.Clear;
        return WeatherCondition.Unknown;
    }

    /// <summary>按英文描述判断。</summary>
    public static WeatherCondition FromEnglish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return WeatherCondition.Unknown;
        }

        var t = text.ToLowerInvariant();
        if (t.Contains("thunder")) return WeatherCondition.Thunderstorm;
        if (t.Contains("hail") || t.Contains("ice pellet")) return WeatherCondition.Hail;
        if (t.Contains("freezing") || t.Contains("sleet")) return WeatherCondition.Sleet;
        if (t.Contains("blizzard") || t.Contains("snow") || t.Contains("flurr")) return WeatherCondition.Snow;
        if (t.Contains("fog") || t.Contains("mist")) return WeatherCondition.Fog;
        if (t.Contains("haze") || t.Contains("smoke") || t.Contains("dust") || t.Contains("sand")) return WeatherCondition.Haze;
        if (t.Contains("heavy rain") || t.Contains("heavy rain")) return WeatherCondition.HeavyRain;
        if (t.Contains("drizzle")) return WeatherCondition.Drizzle;
        if (t.Contains("rain") || t.Contains("shower")) return WeatherCondition.Rain;
        if (t.Contains("overcast")) return WeatherCondition.Overcast;
        // 「partly cloudy」也含「cloudy」，所以必须先判 partly
        if (t.Contains("partly") || t.Contains("mostly clear") || t.Contains("scattered")) return WeatherCondition.PartlyCloudy;
        if (t.Contains("cloudy")) return WeatherCondition.Cloudy;
        if (t.Contains("clear") || t.Contains("sunny") || t.Contains("fair")) return WeatherCondition.Clear;
        return WeatherCondition.Unknown;
    }

    /// <summary>中英文都试，中文优先。</summary>
    public static WeatherCondition FromAny(string? text)
    {
        var chinese = FromChinese(text);
        return chinese != WeatherCondition.Unknown ? chinese : FromEnglish(text);
    }
}
