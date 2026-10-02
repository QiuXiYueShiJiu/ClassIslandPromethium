// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.WeatherProviders;

namespace ClassIsland.Promethium.Services;

/// <summary>一个数据源在界面上的说明。</summary>
/// <param name="Kind">标识。</param>
/// <param name="Name">名字。</param>
/// <param name="Description">优点与已知短板。</param>
/// <param name="RequiresApiKey">是否需要密钥。</param>
/// <param name="ApiKeyUrl">去哪申请密钥；免密钥的为空串。</param>
/// <param name="Region">覆盖范围。</param>
public record WeatherProviderInfo(
    WeatherProviderKind Kind,
    string Name,
    string Description,
    bool RequiresApiKey,
    string ApiKeyUrl,
    string Region);

/// <summary>
/// 所有可用天气数据源的台账，以及按标识取实例。
/// </summary>
/// <remarks>
/// 列表顺序只影响下拉框里的排列（免密钥的排前面更好找）。
/// 设置页绑的是数据源对象本身而不是索引，所以顺序错位不会导致选错源。
/// </remarks>
public class WeatherProviderCatalog
{
    private readonly Dictionary<WeatherProviderKind, IWeatherProvider> _providers;

    /// <summary>可选数据源清单：免密钥的排前面。</summary>
    public IReadOnlyList<WeatherProviderInfo> All { get; } = new[]
    {
        // ---------- 免密钥 ----------
        new WeatherProviderInfo(WeatherProviderKind.OpenMeteo, "Open-Meteo",
            "默认。直接按经纬度查，返回 WMO 标准码，映射关系清楚。免费额度对个人使用足够。",
            false, string.Empty, "全球"),
        new WeatherProviderInfo(WeatherProviderKind.MetNorway, "MET Norway",
            "挪威气象研究所。数据质量高、接口稳定；不提供体感温度，插件会退化成用气温显示。",
            false, string.Empty, "全球"),
        new WeatherProviderInfo(WeatherProviderKind.WttrIn, "wttr.in",
            "配起来最省事，但它本身是聚合服务，底层数据源不透明，数值偶尔和别家对不上。",
            false, string.Empty, "全球"),
        new WeatherProviderInfo(WeatherProviderKind.SevenTimer, "7Timer",
            "免密钥。但它是 3 小时一档的预报而不是观测，「当前」最多能差 3 小时；风速只给蒲福风级；降水量给的是等级码不是毫米，所以本插件不取它的降水量。",
            false, string.Empty, "全球"),
        new WeatherProviderInfo(WeatherProviderKind.Nws, "NWS（仅美国）",
            "美国国家气象局官方数据，质量高、免密钥。只覆盖美国，其他坐标会直接报错。",
            false, string.Empty, "仅美国"),

        new WeatherProviderInfo(WeatherProviderKind.BrightSky, "DWD 德国",
            "德国气象局观测，经纬度直查、免密钥。只覆盖德国，其他坐标会给出提示。不给当日温度范围和体感温度。",
            false, string.Empty, "仅德国"),
        new WeatherProviderInfo(WeatherProviderKind.NeaSingapore, "NEA 新加坡",
            "新加坡国家环境局，免密钥。逐个要素并发取最近站点；它没有天空状况字段，所以天气现象只能靠有没有降雨推断。只覆盖新加坡。",
            false, string.Empty, "仅新加坡"),
        new WeatherProviderInfo(WeatherProviderKind.Estonia, "爱沙尼亚",
            "爱沙尼亚气象局，免密钥。一次拉回全国站点挑最近的，观测站较稀疏（可能几十公里外）。只覆盖爱沙尼亚。",
            false, string.Empty, "仅爱沙尼亚"),

        // ---------- 需要密钥 ----------
        new WeatherProviderInfo(WeatherProviderKind.QWeather, "和风天气",
            "国内可用性最好的一个，中文描述、数据细。需要自己去官网申请免费密钥。注意它给账号分配专属 API Host 时本预设可能连不上。",
            true, "https://dev.qweather.com/", "全球（国内优化）"),
        new WeatherProviderInfo(WeatherProviderKind.Seniverse, "心知天气",
            "国内另一家常用服务，中文描述。需要自己申请密钥。",
            true, "https://www.seniverse.com/", "全球（国内优化）"),
        new WeatherProviderInfo(WeatherProviderKind.OpenWeatherMap, "OpenWeatherMap",
            "最老牌的一家，免费额度够用。当前天气接口不含当日温度范围。需要密钥。",
            true, "https://openweathermap.org/api", "全球"),
        new WeatherProviderInfo(WeatherProviderKind.WeatherApi, "WeatherAPI",
            "一次请求同时给当前天气和当天温度范围，还直接给白天黑夜标志。需要密钥。",
            true, "https://www.weatherapi.com/", "全球"),
        new WeatherProviderInfo(WeatherProviderKind.Weatherbit, "Weatherbit",
            "需要密钥。当前天气接口不含当日温度范围。",
            true, "https://www.weatherbit.io/", "全球"),
        new WeatherProviderInfo(WeatherProviderKind.VisualCrossing, "Visual Crossing",
            "一次请求给当前天气、当天温度范围和日出日落。需要密钥。",
            true, "https://www.visualcrossing.com/weather-api", "全球"),
        new WeatherProviderInfo(WeatherProviderKind.TomorrowIo, "Tomorrow.io",
            "给的是数值天气码。需要密钥，免费额度偏紧，适合当备选。",
            true, "https://www.tomorrow.io/", "全球")
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
