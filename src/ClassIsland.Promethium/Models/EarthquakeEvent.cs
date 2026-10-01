// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>按震级和震中距给出的粗略震感判断。</summary>
public enum FeltLikelihood
{
    /// <summary>基本无感。</summary>
    Unlikely = 0,

    /// <summary>可能有轻微震感。</summary>
    Slight = 1,

    /// <summary>可能有明显震感。</summary>
    Strong = 2
}

/// <summary>
/// 一条真实发生过的地震记录。
/// </summary>
/// <remarks>
/// 除了 <see cref="FeltLikelihood"/> 是由震级和震中距按经验规则粗判出来的，
/// 其余字段都是数据源原样给的观测结果，没有经过任何加工。
/// </remarks>
public class EarthquakeEvent
{
    /// <summary>数据源里的事件编号，用来去重。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>震级。</summary>
    public double Magnitude { get; set; }

    /// <summary>地点描述（数据源原文）。</summary>
    public string Place { get; set; } = string.Empty;

    /// <summary>发震时刻（本地时间）。</summary>
    public DateTimeOffset Time { get; set; }

    /// <summary>震中纬度。</summary>
    public double Latitude { get; set; }

    /// <summary>震中经度。</summary>
    public double Longitude { get; set; }

    /// <summary>震源深度（公里）。</summary>
    public double DepthKm { get; set; }

    /// <summary>数据源上这条事件的页面。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>震中到关注点的距离（公里）。</summary>
    public double DistanceKm { get; set; }

    /// <summary>粗略震感判断。</summary>
    public FeltLikelihood Felt { get; set; }

    /// <summary>震感的文字说法。</summary>
    public string FeltText => Felt switch
    {
        FeltLikelihood.Strong => "可能有明显震感",
        FeltLikelihood.Slight => "可能有轻微震感",
        _ => "基本无感"
    };
}
