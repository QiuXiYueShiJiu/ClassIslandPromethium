// Pm钷 —— ClassIsland 综合增强插件
using System.Net.Http;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.WeatherProviders;

namespace ClassIsland.Promethium.Services;

/// <summary>自动配置的结果，用来告诉用户到底选成了什么。</summary>
/// <param name="WeatherProviderName">选中的天气数据源；没探到则为空。</param>
/// <param name="MapSourceName">选中的底图；没探到则为空。</param>
/// <param name="Summary">一句话结论。</param>
public record AutoConfigResult(string WeatherProviderName, string MapSourceName, string Summary);

/// <summary>
/// 替用户把配置试出来。
/// </summary>
/// <remarks>
/// 这个插件的选项太多（十几个数据源、七种底图），而**哪条路通完全取决于用户在哪个网络里**——
/// 国内的服务到国外连不上，国外的服务在国内也常常不通。让用户自己去试是件很折磨的事，
/// 所以提供一个「一键自动配置」：挨个试一遍，把能用的挑出来。
/// <para/>
/// 只在<b>免密钥</b>的数据源里挑：需要密钥的没法自动配，得用户自己去申请。
/// 也只在<b>全球性</b>的数据源里挑，国家级的那几个只覆盖本国，随便挑会给出别国的天气。
/// </remarks>
public class AutoConfigurator
{
    /// <summary>
    /// 瓦片小于这个字节数就认为是个空白占位图。
    /// </summary>
    /// <remarks>
    /// 实测数据：真瓦片十几到二十几 KB；百度/腾讯拒绝未授权请求时返回的是
    /// 169 和 210 字节的单色占位图。1 KB 这个阈值把两类分得很开。
    /// </remarks>
    private const int MinimumTileBytes = 1024;

    private static readonly HttpClient Http = HttpClients.Shared;

    /// <summary>参与自动探测的天气数据源，按顺序试，能通就用。</summary>
    private static readonly WeatherProviderKind[] ProbeOrder =
    {
        WeatherProviderKind.OpenMeteo,
        WeatherProviderKind.MetNorway,
        WeatherProviderKind.WttrIn,
        WeatherProviderKind.SevenTimer
    };

    /// <summary>参与自动探测的底图，按顺序试（就是自动模式的优先级）。</summary>
    private static readonly MapTileSource[] MapProbeOrder =
    {
        MapTileSource.AMap,
        MapTileSource.Baidu,
        MapTileSource.Tencent,
        MapTileSource.OpenStreetMap
    };

    private readonly WeatherProviderCatalog _catalog;
    private readonly PromethiumConfigStore _store;

    public AutoConfigurator(WeatherProviderCatalog catalog, PromethiumConfigStore store)
    {
        _catalog = catalog;
        _store = store;
    }

    /// <summary>跑一遍自动配置，直接改配置对象。</summary>
    public async Task<AutoConfigResult> RunAsync(CancellationToken token = default)
    {
        var config = _store.Weather;

        var weatherName = await ProbeWeatherAsync(config, token).ConfigureAwait(false);
        var mapName = await ProbeMapAsync(config, token).ConfigureAwait(false);

        var summary = (weatherName, mapName) switch
        {
            (not null, not null) => $"已自动配置：天气用 {weatherName}，底图用 {mapName}。",
            (not null, null) => $"天气已用 {weatherName}；但四家底图都没能取到有效瓦片，界面会先只显示网格，可稍后重试或改用自定义地址。",
            (null, not null) => $"底图已用 {mapName}；但没探到可用的免密钥天气源，请检查网络后重试。",
            _ => "网络似乎不通：天气源和底图都没探到。请检查网络后重试。"
        };

        return new AutoConfigResult(weatherName ?? string.Empty, mapName ?? string.Empty, summary);
    }

    private async Task<string?> ProbeWeatherAsync(WeatherConfig config, CancellationToken token)
    {
        var saved = config.Provider;
        try
        {
            foreach (var kind in ProbeOrder)
            {
                token.ThrowIfCancellationRequested();
                var provider = _catalog.Resolve(kind);
                try
                {
                    var snapshot = await provider
                        .QueryAsync(new WeatherQuery(config.Latitude, config.Longitude, config.GetApiKey(kind)), token)
                        .ConfigureAwait(false);

                    // 温度拿到 0 说明这次返回不可信，换下一个
                    if (snapshot.Temperature != 0)
                    {
                        config.Provider = kind;
                        return provider.DisplayName;
                    }
                }
                catch (Exception)
                {
                    // 这个源不通，试下一个
                }
            }
        }
        finally
        {
            // 全都不通时不要留下半截改动
            if (config.Provider == saved)
            {
                config.Provider = saved;
            }
        }

        return null;
    }

    private async Task<string?> ProbeMapAsync(WeatherConfig config, CancellationToken token)
    {
        foreach (var source in MapProbeOrder)
        {
            token.ThrowIfCancellationRequested();

            var probeConfig = new WeatherConfig { MapTileSource = source };
            var specs = MapTileCatalog.ResolveChain(probeConfig);
            if (specs.Count == 0 || !specs[0].HasBasemap)
            {
                continue;
            }

            var spec = specs[0];
            var (x, y) = TileMath.TileOf(config.Latitude, config.Longitude, 12, spec.Datum);
            var url = MapTileCatalog.BuildUrl(spec, string.Empty, 12, x, y);
            if (url == null)
            {
                continue;
            }

            try
            {
                using var response = await Http.GetAsync(url, token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                if (bytes.Length >= MinimumTileBytes)
                {
                    config.MapTileSource = source;
                    return spec.Name;
                }
            }
            catch (Exception)
            {
                // 这个底图不通，试下一个
            }
        }

        return null;
    }
}
