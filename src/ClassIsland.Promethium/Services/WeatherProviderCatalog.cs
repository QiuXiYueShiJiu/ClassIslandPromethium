// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.WeatherProviders;

namespace ClassIsland.Promethium.Services;

/// <summary>一个数据源在界面上的说明。</summary>
/// <param name="Kind">标识。</param>
/// <param name="Name">名字。</param>
/// <param name="Description">优点与已知问题。</param>
/// <param name="RequiresApiKey">是否需要密钥。</param>
public record WeatherProviderInfo(WeatherProviderKind Kind, string Name, string Description, bool RequiresApiKey);

/// <summary>
/// 所有可用天气数据源的台账，以及按标识取实例。
/// </summary>
public class WeatherProviderCatalog
{
    private readonly Dictionary<WeatherProviderKind, IWeatherProvider> _providers;

    /// <summary>可选数据源清单。</summary>
    public IReadOnlyList<WeatherProviderInfo> All { get; } = new[]
    {
        new WeatherProviderInfo(WeatherProviderKind.OpenMeteo, "Open-Meteo",
            "默认。直接按经纬度查，返回 WMO 标准码，映射关系清楚。免费额度对个人使用足够。", false),
        new WeatherProviderInfo(WeatherProviderKind.MetNorway, "MET Norway",
            "挪威气象研究所。数据质量高、接口稳定；不提供体感温度，插件会退化成用气温显示。", false),
        new WeatherProviderInfo(WeatherProviderKind.WttrIn, "wttr.in",
            "配起来最省事，但它本身是聚合服务，底层数据源不透明，数值偶尔和别家对不上。", false)
    };

    public WeatherProviderCatalog(IEnumerable<IWeatherProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Kind);
    }

    /// <summary>按标识取数据源；取不到就退回默认的 Open-Meteo。</summary>
    public IWeatherProvider Resolve(WeatherProviderKind kind) =>
        _providers.TryGetValue(kind, out var provider)
            ? provider
            : _providers[WeatherProviderKind.OpenMeteo];
}
