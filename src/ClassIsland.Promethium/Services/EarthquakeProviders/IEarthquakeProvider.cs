// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.EarthquakeProviders;

/// <summary>一次地震目录查询的条件。</summary>
/// <param name="Latitude">关注点纬度。</param>
/// <param name="Longitude">关注点经度。</param>
/// <param name="RadiusKm">关注半径（公里）。</param>
/// <param name="MinMagnitude">最低震级。</param>
/// <param name="LookbackHours">回看多少小时。</param>
public record EarthquakeQuery(
    double Latitude, double Longitude, double RadiusKm, double MinMagnitude, double LookbackHours);

/// <summary>一个地震目录数据源。</summary>
public interface IEarthquakeProvider
{
    /// <summary>数据源标识。</summary>
    EarthquakeSource Source { get; }

    /// <summary>界面上显示的名字。</summary>
    string DisplayName { get; }

    /// <summary>查最近的地震。实现方负责保证返回的都是真实记录。</summary>
    Task<IReadOnlyList<EarthquakeEvent>> QueryRecentAsync(EarthquakeQuery query, CancellationToken token = default);
}
