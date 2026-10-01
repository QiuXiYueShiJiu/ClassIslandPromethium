// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
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

/// <summary>可选的天气数据源。</summary>
public enum WeatherProviderKind
{
    /// <summary>Open-Meteo，免密钥。</summary>
    OpenMeteo = 0,

    /// <summary>挪威气象研究所，免密钥。</summary>
    MetNorway = 1,

    /// <summary>wttr.in，免密钥。</summary>
    WttrIn = 2
}
