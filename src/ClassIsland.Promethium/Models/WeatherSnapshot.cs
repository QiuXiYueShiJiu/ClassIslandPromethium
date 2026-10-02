// Pm钷 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>一次天气查询的结果快照。</summary>
public class WeatherSnapshot
{
    /// <summary>这条数据是哪个数据源给的。</summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>观测时间。</summary>
    public DateTimeOffset ObservedAt { get; set; }

    /// <summary>归一化后的天气现象。</summary>
    public WeatherCondition Condition { get; set; } = WeatherCondition.Unknown;

    /// <summary>数据源自己的天气代码原文，便于核对。</summary>
    public string RawCode { get; set; } = string.Empty;

    /// <summary>气温（摄氏度）。</summary>
    public double Temperature { get; set; }

    /// <summary>体感温度（摄氏度）。未提供时等于气温。</summary>
    public double FeelsLike { get; set; }

    /// <summary>相对湿度（%）。</summary>
    public double Humidity { get; set; }

    /// <summary>风速（km/h）。</summary>
    public double WindSpeed { get; set; }

    /// <summary>风向（度，0 为正北）。未提供时为 -1。</summary>
    public double WindDirection { get; set; } = -1d;

    /// <summary>海平面气压（hPa）。</summary>
    public double Pressure { get; set; }

    /// <summary>降水量（mm）。</summary>
    public double Precipitation { get; set; }

    /// <summary>当时是否为白天。</summary>
    public bool IsDay { get; set; } = true;

    /// <summary>今日最低温。</summary>
    public double TodayMin { get; set; }

    /// <summary>今日最高温。</summary>
    public double TodayMax { get; set; }

    /// <summary>数据源是否提供了今日温度范围。</summary>
    public bool HasDailyRange { get; set; }
}
