// Pm钷 —— ClassIsland 综合增强插件
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Models;

/// <summary>
/// 「更好的天气」的全部全局配置：看哪儿、用哪个数据源、图标怎么画、什么时候报警。
/// </summary>
/// <remarks>
/// 位置放全局而不是组件设置里，是照着宿主的分工来的——宿主也是「城市在设置窗口、
/// 显示什么在组件设置里」。这样加两个天气组件不会各记一个城市，也不至于找不着地方改。
/// </remarks>
public partial class WeatherConfig : ObservableObject
{
    // ---------- 位置 ----------

    /// <summary>纬度。</summary>
    [ObservableProperty]
    private double _latitude = 39.9042;

    /// <summary>经度。</summary>
    [ObservableProperty]
    private double _longitude = 116.4074;

    /// <summary>地点显示名，由逆地理编码反查到街道。</summary>
    [ObservableProperty]
    private string _locationName = "北京市东城区";

    /// <summary>
    /// 用户是否动手选过位置。
    /// </summary>
    /// <remarks>
    /// 默认给一个中性位置（北京市中心）只是为了让界面一打开就有东西可看，
    /// 并不代表用户在那儿。这个标记用来提示他还没选过自己的位置。
    /// </remarks>
    [ObservableProperty]
    private bool _isLocationPicked;

    // ---------- 数据源 ----------

    /// <summary>用哪个天气数据源。</summary>
    [ObservableProperty]
    private WeatherProviderKind _provider = WeatherProviderKind.OpenMeteo;

    /// <summary>
    /// 各数据源的密钥，按数据源名分开存。
    /// </summary>
    /// <remarks>
    /// 分开存是为了让用户在几个源之间来回切时不用反复粘密钥。
    /// 免密钥的源不会往这里写东西。
    /// </remarks>
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    /// <summary>取某个数据源的密钥。</summary>
    public string GetApiKey(WeatherProviderKind kind) =>
        ApiKeys.TryGetValue(kind.ToString(), out var key) ? key : string.Empty;

    /// <summary>存某个数据源的密钥。</summary>
    public void SetApiKey(WeatherProviderKind kind, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            ApiKeys.Remove(kind.ToString());
        }
        else
        {
            ApiKeys[kind.ToString()] = key;
        }

        OnPropertyChanged(nameof(ApiKeys));
    }

    // ---------- 图标显示方式 ----------

    /// <summary>天气图标怎么画。</summary>
    [ObservableProperty]
    private WeatherIconMode _iconMode = WeatherIconMode.SystemGlyph;

    /// <summary>emoji 模式用的字体。</summary>
    [ObservableProperty]
    private string _emojiFontFamily = "Segoe UI Emoji";

    // ---------- 地名服务 ----------

    /// <summary>
    /// 地名反查用哪一家。默认自动降级：高德（有密钥时）→ Nominatim → Photon → BigDataCloud。
    /// </summary>
    [ObservableProperty]
    private GeocodingProviderKind _geocodingProvider = GeocodingProviderKind.Auto;

    /// <summary>高德密钥。填了它就用高德做地名反查与搜索，国内最好用。</summary>
    [ObservableProperty]
    private string _amapGeocodingKey = string.Empty;

    // ---------- 地图底图 ----------

    /// <summary>
    /// 底图用哪一家。默认按优先级自动挑：高德 → 百度 → 腾讯 → OpenStreetMap。
    /// </summary>
    [ObservableProperty]
    private MapTileSource _mapTileSource = MapTileSource.Auto;

    /// <summary>自定义底图地址模板，须含 {z} {x} {y}。</summary>
    [ObservableProperty]
    private string _customTileUrl = string.Empty;

    /// <summary>自定义底图要显示的版权署名。</summary>
    [ObservableProperty]
    private string _customTileAttribution = string.Empty;

    /// <summary>自定义底图用的坐标基准，填错了标记会偏。</summary>
    [ObservableProperty]
    private TileDatum _customTileDatum = TileDatum.Wgs84;

    // ---------- 图标 ----------

    /// <summary>自定义图片模式用的目录。</summary>
    [ObservableProperty]
    private string _customImageFolder = string.Empty;

    /// <summary>自定义图片模式下图标显示高度（像素）。</summary>
    [ObservableProperty]
    private double _customImageHeight = 24d;

    // ---------- 报警 ----------

    /// <summary>高温报警阈值（摄氏度）。</summary>
    [ObservableProperty]
    private double _highTemperatureThreshold = 35d;

    /// <summary>低温报警阈值（摄氏度）。</summary>
    [ObservableProperty]
    private double _lowTemperatureThreshold = -10d;

    /// <summary>大风报警阈值（km/h）。</summary>
    [ObservableProperty]
    private double _windSpeedThreshold = 62d;

    /// <summary>单日降水量报警阈值（mm）。</summary>
    [ObservableProperty]
    private double _precipitationThreshold = 50d;

    /// <summary>雷暴天气是否报警。</summary>
    [ObservableProperty]
    private bool _alertThunderstorm = true;

    /// <summary>大雾是否报警。</summary>
    [ObservableProperty]
    private bool _alertFog = true;

    /// <summary>报警送到哪儿。</summary>
    [ObservableProperty]
    private AlertDelivery _delivery = AlertDelivery.Both;

    /// <summary>提醒文案模板，支持 {变量}。</summary>
    [ObservableProperty]
    private string _notifyTemplate = "{位置} {标题}：{详情}";

    /// <summary>组件上实时显示的文案模板，支持 {变量}。</summary>
    [ObservableProperty]
    private string _inlineTemplate = "{标题}";

    /// <summary>报警图标字形。</summary>
    [ObservableProperty]
    private string _alertGlyph = "\uE7BA";

    /// <summary>自定义提示音文件路径，留空表示只用宿主通知自己的声音。</summary>
    [ObservableProperty]
    private string _soundPath = string.Empty;

    /// <summary>自定义提示音音量（0~1）。</summary>
    [ObservableProperty]
    private double _soundVolume = 1d;

    /// <summary>
    /// 天气刷新间隔（分钟）。放全局是因为多个组件共享同一次抓取，
    /// 没必要每个组件各查一遍。小于等于 0 表示不自动刷新。
    /// </summary>
    [ObservableProperty]
    private double _refreshIntervalMinutes = 15d;

    /// <summary>
    /// 已经报过的报警（形如 <c>high_temperature|2026-10-02</c>）。
    /// 同一类报警同一天只报一次，免得到点刷新一次就弹一次。
    /// </summary>
    public List<string> NotifiedAlertKeys { get; set; } = new();
}
