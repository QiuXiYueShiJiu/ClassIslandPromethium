// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.EarthquakeProviders;

namespace ClassIsland.Promethium.Services;

/// <summary>一个地震目录数据源在界面上的说明。</summary>
/// <param name="Source">标识。</param>
/// <param name="Name">名字。</param>
/// <param name="Description">特点与已知局限。</param>
public record EarthquakeProviderInfo(EarthquakeSource Source, string Name, string Description);

/// <summary>地震目录数据源的台账。</summary>
public class EarthquakeProviderCatalog
{
    private readonly Dictionary<EarthquakeSource, IEarthquakeProvider> _providers;

    /// <summary>可选数据源清单。</summary>
    public IReadOnlyList<EarthquakeProviderInfo> All { get; } = new[]
    {
        new EarthquakeProviderInfo(EarthquakeSource.Usgs, "USGS",
            "默认。美国地质调查局全球目录，原生支持按公里半径与震级筛选。国内小震可能漏报。"),
        new EarthquakeProviderInfo(EarthquakeSource.Emsc, "EMSC",
            "欧洲-地中海地震中心，对欧亚大陆一带记录较全。接口只收度作半径，插件内部换算成包围盒再按真实距离筛。")
    };

    public EarthquakeProviderCatalog(IEnumerable<IEarthquakeProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Source);
    }

    /// <summary>按标识取数据源；取不到就退回 USGS。</summary>
    public IEarthquakeProvider Resolve(EarthquakeSource source) =>
        _providers.TryGetValue(source, out var provider) ? provider : _providers[EarthquakeSource.Usgs];
}
