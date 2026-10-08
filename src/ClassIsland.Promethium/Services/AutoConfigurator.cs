// Pm钷 —— ClassIsland 综合增强插件
using System.Net.Http;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.WeatherProviders;

namespace ClassIsland.Promethium.Services;

/// <summary>自动配置探测出来的结果。</summary>
/// <param name="WeatherProvider">探到的天气数据源；没探到为 null。</param>
/// <param name="WeatherProviderName">它的显示名。</param>
/// <param name="MapSource">探到的底图；没探到为 null。</param>
/// <param name="MapSourceName">它的显示名。</param>
/// <param name="Summary">一句话结论。</param>
public record AutoConfigResult(
    WeatherProviderKind? WeatherProvider,
    string WeatherProviderName,
    MapTileSource? MapSource,
    string MapSourceName,
    string Summary);

/// <summary>
/// 替用户把配置试出来。
/// </summary>
/// <remarks>
/// 这个插件的选项太多（十几个数据源、七种底图），而**哪条路通完全取决于用户在哪个网络里**——
/// 国内的服务到国外连不上，国外的服务在国内也常常不通。让用户自己去试是件很折磨的事，
/// 所以提供一个「一键自动配置」：挨个试一遍，把能用的挑出来。
/// <para/>
/// <b>它绝不改动传入的配置。</b>只负责探测并返回结果，写配置由调用方在 UI 线程上做。
/// 这是踩过坑之后定的规矩：早先它在后台线程上直接改配置对象，配置一改就触发
/// 界面的 PropertyChanged 处理器，而那个处理器会去碰 Avalonia 控件——
/// 从非 UI 线程碰控件会让整个界面卡死。探测是后台的事，改配置是界面的事，分开就不会再出这个问题。
/// <para/>
/// 只在<b>免密钥</b>的数据源里挑：需要密钥的没法自动配，得用户自己去申请。
/// 也只在<b>全球性</b>的数据源里挑，国家级的那几个只覆盖本国，随便挑会给出别国的天气。
/// </remarks>
public class AutoConfigurator
{
    /// <summary>
    /// 单个源的探测超时。
    /// </summary>
    /// <remarks>
    /// 必须有：某个源如果只是慢（而不是不通），不给上限的话整个探测会拖到
    /// HttpClient 自己的 20 秒超时，四个源加四个底图就是好几分钟，
    /// 用户看到的就是「点了按钮没反应」。
    /// </remarks>
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(6);

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
    private static readonly WeatherProviderKind[] WeatherProbeOrder =
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

    public AutoConfigurator(WeatherProviderCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// 跑一遍探测，返回该用哪些源。
    /// </summary>
    /// <param name="latitude">关注点纬度（WGS84）。</param>
    /// <param name="longitude">关注点经度（WGS84）。</param>
    /// <param name="mapApiKey">需要密钥的底图用（目前没有，留着以后扩展）。</param>
    /// <param name="progress">每试完一家就回报一次，让界面能显示进展而不是干等。</param>
    /// <param name="token">取消令牌。</param>
    public async Task<AutoConfigResult> RunAsync(
        double latitude,
        double longitude,
        string mapApiKey = "",
        IProgress<string>? progress = null,
        CancellationToken token = default)
    {
        var weatherProvider = await ProbeWeatherAsync(latitude, longitude, progress, token).ConfigureAwait(false);
        var mapSource = await ProbeMapAsync(latitude, longitude, mapApiKey, progress, token).ConfigureAwait(false);

        var weatherName = weatherProvider.HasValue ? _catalog.Resolve(weatherProvider.Value).DisplayName : string.Empty;
        var mapName = mapSource.HasValue
            ? MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = mapSource.Value })[0].Name
            : string.Empty;

        var summary = (weatherProvider.HasValue, mapSource.HasValue) switch
        {
            (true, true) => $"已自动配置：天气用 {weatherName}，底图用 {mapName}。",
            (true, false) => $"天气已用 {weatherName}；但四家底图都没能取到有效瓦片，界面会先只显示网格，可稍后重试或改用自定义地址。",
            (false, true) => $"底图已用 {mapName}；但没探到可用的免密钥天气源，请检查网络后重试。",
            _ => "网络似乎不通：天气源和底图都没探到。请检查网络后重试。"
        };

        return new AutoConfigResult(weatherProvider, weatherName, mapSource, mapName, summary);
    }

    private async Task<WeatherProviderKind?> ProbeWeatherAsync(
        double latitude, double longitude, IProgress<string>? progress, CancellationToken token)
    {
        foreach (var kind in WeatherProbeOrder)
        {
            token.ThrowIfCancellationRequested();
            var provider = _catalog.Resolve(kind);
            progress?.Report($"正在试天气源 {provider.DisplayName}…");

            try
            {
                using var probe = CreateProbeToken(token);
                var snapshot = await provider
                    .QueryAsync(new WeatherQuery(latitude, longitude, string.Empty), probe.Token)
                    .ConfigureAwait(false);

                // 温度拿到 0 说明这次返回不可信，换下一个
                if (snapshot.Temperature != 0)
                {
                    return kind;
                }
            }
            catch (Exception)
            {
                // 这个源不通或超时，试下一个
            }
        }

        return null;
    }

    private async Task<MapTileSource?> ProbeMapAsync(
        double latitude, double longitude, string mapApiKey, IProgress<string>? progress, CancellationToken token)
    {
        foreach (var source in MapProbeOrder)
        {
            token.ThrowIfCancellationRequested();

            // 用一份临时配置来解析规则，绝不碰用户真正的那份
            var probeConfig = new WeatherConfig { MapTileSource = source };
            var specs = MapTileCatalog.ResolveChain(probeConfig);
            if (specs.Count == 0 || !specs[0].HasBasemap)
            {
                continue;
            }

            var spec = specs[0];
            progress?.Report($"正在试底图 {spec.Name}…");

            var (x, y) = TileMath.TileOf(latitude, longitude, 12, spec.Datum);
            var url = MapTileCatalog.BuildUrl(spec, mapApiKey, 12, x, y);
            if (url == null)
            {
                continue;
            }

            try
            {
                using var probe = CreateProbeToken(token);
                using var response = await Http.GetAsync(url, probe.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(probe.Token).ConfigureAwait(false);
                if (bytes.Length >= MinimumTileBytes)
                {
                    return source;
                }
            }
            catch (Exception)
            {
                // 这个底图不通或超时，试下一个
            }
        }

        return null;
    }

    /// <summary>
    /// 造一个带超时的探测令牌。
    /// </summary>
    /// <remarks>
    /// 探测超时只代表「这一家不用」，不等于用户取消了整体操作，
    /// 所以外层令牌照常可取消，超时则被上面的 catch 吃掉，继续试下一家。
    /// </remarks>
    private static CancellationTokenSource CreateProbeToken(CancellationToken outer)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(outer);
        source.CancelAfter(ProbeTimeout);
        return source;
    }
}
