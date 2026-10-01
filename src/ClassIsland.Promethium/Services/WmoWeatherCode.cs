// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Services;

/// <summary>
/// 把 WMO 气象码翻译成中文描述和图标字形。
/// </summary>
/// <remarks>
/// 图标码位取自 FluentAvalonia 的 <c>Symbol</c> 枚举里那一组 <c>Weather*</c> 成员，
/// 不是照抄别人插件里的魔数，所以每个码位都能对回来源。
/// </remarks>
public static class WmoWeatherCode
{
    /// <summary>取中文描述。</summary>
    public static string Describe(int code) => code switch
    {
        0 => "晴",
        1 => "晴间多云",
        2 => "多云",
        3 => "阴",
        45 => "有雾",
        48 => "冻雾",
        51 or 53 or 55 => "毛毛雨",
        56 or 57 => "冻毛毛雨",
        61 => "小雨",
        63 => "中雨",
        65 => "大雨",
        66 or 67 => "冻雨",
        71 => "小雪",
        73 => "中雪",
        75 => "大雪",
        77 => "米雪",
        80 => "阵雨",
        81 => "强阵雨",
        82 => "暴雨",
        85 => "阵雪",
        86 => "强阵雪",
        95 => "雷阵雨",
        96 or 99 => "雷阵雨伴冰雹",
        _ => "未知"
    };

    /// <summary>取 Fluent 图标字形。</summary>
    /// <param name="code">WMO 气象码。</param>
    /// <param name="isDay">白天用日间图标，夜间用夜间图标。</param>
    public static string Glyph(int code, bool isDay) => code switch
    {
        0 => isDay ? "\uF811A" : "\uF8102",                 // 晴 / 晴夜
        1 => isDay ? "\uF811E" : "\uF8106",                 // 晴间多云
        2 => isDay ? "\uF8104" : "\uF8106",                 // 多云
        3 => "\uF80F4",                                      // 阴
        45 or 48 => "\uF80FA",                               // 雾
        51 or 53 or 55 => "\uF80F6",                         // 毛毛雨
        56 or 57 => "\uF810E",                               // 冻毛毛雨
        61 or 63 or 65 => "\uF8108",                         // 雨
        66 or 67 => "\uF810E",                               // 冻雨
        71 or 73 or 75 => "\uF8110",                         // 雪
        77 => "\uF8112",                                     // 米雪
        80 or 81 or 82 => isDay ? "\uF810A" : "\uF810C",     // 阵雨
        85 or 86 => isDay ? "\uF8114" : "\uF8116",           // 阵雪
        95 => "\uF8120",                                     // 雷阵雨
        96 or 99 => isDay ? "\uF80FC" : "\uF80FE",           // 冰雹
        _ => "\uF80F4"
    };

    /// <summary>把风向角度转成中文方位，用于文字显示。</summary>
    public static string Direction(double degrees)
    {
        string[] names = { "北", "东北偏北", "东北", "东北偏东", "东", "东南偏东", "东南", "东南偏南",
                           "南", "西南偏南", "西南", "西南偏西", "西", "西北偏西", "西北", "西北偏北" };
        var index = (int)Math.Round(((degrees % 360) + 360) % 360 / 22.5) % 16;
        return names[index];
    }
}
