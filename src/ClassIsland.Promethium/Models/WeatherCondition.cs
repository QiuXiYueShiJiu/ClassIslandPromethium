// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 归一化之后的天气现象。
/// </summary>
/// <remarks>
/// 各家数据源的天气代码体系都不一样（WMO、MET 的 symbol_code、WWO 的编号……），
/// 如果让上层去认某一家的代码，换个数据源就得改一遍界面。
/// 所以每家各自把自己那套映射到这里，界面只认这个。
/// </remarks>
public enum WeatherCondition
{
    Unknown = 0,
    Clear,
    PartlyCloudy,
    Cloudy,
    Overcast,
    Fog,
    Haze,
    Drizzle,
    Rain,
    HeavyRain,
    Sleet,
    Snow,
    Thunderstorm,
    Hail
}

/// <summary>
/// 可选的天气数据源。
/// </summary>
/// <remarks>
/// 枚举顺序就是设置页下拉框的顺序：先把免密钥的排前面，需要密钥的排后面。
/// </remarks>
public enum WeatherProviderKind
{
    // ---------- 免密钥 ----------

    /// <summary>Open-Meteo，全球，WMO 标准码。</summary>
    OpenMeteo = 0,

    /// <summary>挪威气象研究所，全球，质量高。</summary>
    MetNorway = 1,

    /// <summary>wttr.in，全球，聚合服务。</summary>
    WttrIn = 2,

    /// <summary>7Timer，全球，3 小时粒度预报。</summary>
    SevenTimer = 3,

    /// <summary>美国国家气象局，免密钥，仅覆盖美国。</summary>
    Nws = 4,

    // ---------- 需要密钥 ----------

    /// <summary>和风天气，国内可用性好。</summary>
    QWeather = 5,

    /// <summary>心知天气，国内。</summary>
    Seniverse = 6,

    /// <summary>OpenWeatherMap。</summary>
    OpenWeatherMap = 7,

    /// <summary>WeatherAPI.com。</summary>
    WeatherApi = 8,

    /// <summary>Weatherbit。</summary>
    Weatherbit = 9,

    /// <summary>Visual Crossing。</summary>
    VisualCrossing = 10,

    /// <summary>Tomorrow.io。</summary>
    TomorrowIo = 11,

    // ---------- 各国气象部门（免密钥）----------
    // 新增值一律追加在末尾：已有值改了数字，用户存下来的选择就会指到别的源上。

    /// <summary>德国气象局（DWD），经 Bright Sky 提供，经纬度直查。</summary>
    BrightSky = 12,

    /// <summary>新加坡国家环境局（NEA），按最近站点取数。</summary>
    NeaSingapore = 13,

    /// <summary>爱沙尼亚气象局，按最近站点取数。</summary>
    Estonia = 14
}
