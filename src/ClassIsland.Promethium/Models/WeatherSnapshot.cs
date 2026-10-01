// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>一次天气查询的结果快照。</summary>
public class WeatherSnapshot
{
    /// <summary>观测时间。</summary>
    public DateTimeOffset ObservedAt { get; set; }

    /// <summary>气温（摄氏度）。</summary>
    public double Temperature { get; set; }

    /// <summary>体感温度（摄氏度）。</summary>
    public double FeelsLike { get; set; }

    /// <summary>相对湿度（%）。</summary>
    public double Humidity { get; set; }

    /// <summary>风速（km/h）。</summary>
    public double WindSpeed { get; set; }

    /// <summary>风向（度，0 为正北）。</summary>
    public double WindDirection { get; set; }

    /// <summary>海平面气压（hPa）。</summary>
    public double Pressure { get; set; }

    /// <summary>降水量（mm）。</summary>
    public double Precipitation { get; set; }

    /// <summary>WMO 气象码。</summary>
    public int WeatherCode { get; set; }

    /// <summary>当时是否为白天（决定用日间还是夜间图标）。</summary>
    public bool IsDay { get; set; } = true;

    /// <summary>今日最低温。</summary>
    public double TodayMin { get; set; }

    /// <summary>今日最高温。</summary>
    public double TodayMax { get; set; }
}
