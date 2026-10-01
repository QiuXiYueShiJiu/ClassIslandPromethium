// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.ComponentModel;
using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.ViewModels;

/// <summary>
/// 「更好的天气」组件显示用的视图模型。
/// </summary>
/// <remarks>
/// 它不抓数据，只把 <see cref="WeatherMonitor"/> 拿到的那份画出来。
/// 抓取和判定都收在监测器里，组件多了也不会重复请求。
/// </remarks>
public partial class BetterWeatherViewModel : ObservableObject
{
    private readonly WeatherMonitor _monitor;
    private readonly PromethiumConfigStore _store;
    private BetterWeatherSettings? _settings;
    private string? _cachedImagePath;
    private Bitmap? _cachedImage;

    /// <summary>天气相关的全局配置。</summary>
    public WeatherConfig Config { get; }

    /// <summary>当前报警状态。</summary>
    public AlertCenter Alerts { get; }

    /// <summary>组件自己的显示设置。宿主构造完之后才注入，所以可能为 null。</summary>
    public BetterWeatherSettings? Settings => _settings;

    // ---------- 生效后的显示开关 ----------
    // 界面绑这些，不直接绑 Settings.ShowX：显示模式为预设时，
    // 实际显示什么是按预设算出来的，与逐项开关无关。

    /// <summary>是否显示位置名（生效值）。</summary>
    [ObservableProperty]
    private bool _showLocationNameEffective = true;

    /// <summary>是否显示今日温差（生效值）。</summary>
    [ObservableProperty]
    private bool _showDailyRangeEffective = true;

    /// <summary>是否显示湿度（生效值）。</summary>
    [ObservableProperty]
    private bool _showHumidityEffective = true;

    /// <summary>是否显示风力（生效值）。</summary>
    [ObservableProperty]
    private bool _showWindEffective;

    /// <summary>是否显示体感温度（生效值）。</summary>
    [ObservableProperty]
    private bool _showFeelsLikeEffective;

    /// <summary>天气报警用的图标字形。</summary>
    public string WeatherAlertGlyph => Config.AlertGlyph;

    /// <summary>地震速报用的图标字形。</summary>
    public string EarthquakeAlertGlyph => _store.Earthquake.AlertGlyph;

    // ---------- 天气文字 ----------

    /// <summary>天气现象文字。名字不叫 WeatherText 是为了不和同名的静态工具类撞。</summary>
    [ObservableProperty]
    private string _conditionText = "等待数据";

    /// <summary>气温文字。</summary>
    [ObservableProperty]
    private string _temperatureText = "--°";

    /// <summary>体感温度文字。</summary>
    [ObservableProperty]
    private string _feelsLikeText = "体感 --°";

    /// <summary>湿度文字。</summary>
    [ObservableProperty]
    private string _humidityText = "湿度 --";

    /// <summary>风力文字。</summary>
    [ObservableProperty]
    private string _windText = "风 --";

    /// <summary>今日温度范围文字。</summary>
    [ObservableProperty]
    private string _todayRangeText = string.Empty;

    /// <summary>提示 / 错误信息。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    // ---------- 图标 ----------

    /// <summary>系统字形。</summary>
    [ObservableProperty]
    private string _iconGlyph = "\uF80F4";

    /// <summary>emoji。</summary>
    [ObservableProperty]
    private string _iconEmoji = "❓";

    /// <summary>自定义图片。</summary>
    [ObservableProperty]
    private Bitmap? _iconImage;

    /// <summary>emoji 用的字体。</summary>
    [ObservableProperty]
    private FontFamily _iconFont = new("Segoe UI Emoji");

    /// <summary>自定义图片的高度。</summary>
    [ObservableProperty]
    private double _iconImageHeight = 24d;

    /// <summary>当前是不是画系统字形。</summary>
    [ObservableProperty]
    private bool _showGlyph = true;

    /// <summary>当前是不是画 emoji。</summary>
    [ObservableProperty]
    private bool _showEmoji;

    /// <summary>当前是不是画自定义图片。</summary>
    [ObservableProperty]
    private bool _showImage;

    public BetterWeatherViewModel(WeatherMonitor monitor, PromethiumConfigStore store, AlertCenter alerts)
    {
        _monitor = monitor;
        _store = store;
        Config = store.Weather;
        Alerts = alerts;

        Config.PropertyChanged += OnConfigChanged;
        store.Earthquake.PropertyChanged += OnEarthquakeConfigChanged;
        _monitor.SnapshotUpdated += (_, _) => Apply(_monitor.Snapshot);
        _monitor.PropertyChanged += OnMonitorChanged;

        ApplyIconMode();
        if (_monitor.Snapshot != null)
        {
            Apply(_monitor.Snapshot);
        }
    }

    /// <summary>关联组件设置。宿主在构造之后才注入，所以要能被反复调用。</summary>
    public void AttachSettings(BetterWeatherSettings settings)
    {
        if (ReferenceEquals(_settings, settings))
        {
            ApplyDisplayMode();
            return;
        }

        if (_settings != null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
        }

        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
        ApplyDisplayMode();
    }

    /// <summary>
    /// 按显示模式算出实际要显示哪几项。
    /// </summary>
    /// <remarks>
    /// 选预设时逐项开关一律不参与计算——否则会出现「模式选了紧凑、
    /// 却因为某个开关还开着而多显示一项」这种说不清的状态。
    /// </remarks>
    private void ApplyDisplayMode()
    {
        var settings = _settings;
        if (settings == null)
        {
            return;
        }

        switch (settings.DisplayMode)
        {
            case WeatherDisplayMode.Compact:
                SetEffective(false, false, false, false, false);
                break;
            case WeatherDisplayMode.Detailed:
                SetEffective(true, true, true, true, true);
                break;
            case WeatherDisplayMode.Custom:
                SetEffective(settings.ShowLocationName, settings.ShowDailyRange, settings.ShowHumidity,
                    settings.ShowWind, settings.ShowFeelsLike);
                break;
            default:
                SetEffective(true, true, true, false, false);
                break;
        }
    }

    private void SetEffective(bool locationName, bool dailyRange, bool humidity, bool wind, bool feelsLike)
    {
        ShowLocationNameEffective = locationName;
        ShowDailyRangeEffective = dailyRange;
        ShowHumidityEffective = humidity;
        ShowWindEffective = wind;
        ShowFeelsLikeEffective = feelsLike;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e) => ApplyDisplayMode();

    /// <summary>立刻要一次数据，用于刚改完设置的时候。</summary>
    public Task RefreshAsync() => _monitor.RefreshNowAsync();

    private void OnEarthquakeConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EarthquakeConfig.AlertGlyph))
        {
            OnPropertyChanged(nameof(EarthquakeAlertGlyph));
        }
    }

    private void OnMonitorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WeatherMonitor.ErrorText))
        {
            StatusText = _monitor.ErrorText;
        }
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeatherConfig.IconMode) or nameof(WeatherConfig.EmojiFontFamily)
            or nameof(WeatherConfig.CustomImageFolder) or nameof(WeatherConfig.CustomImageHeight))
        {
            ApplyIconMode();
            if (_monitor.Snapshot != null)
            {
                Apply(_monitor.Snapshot);
            }
        }

        if (e.PropertyName is nameof(WeatherConfig.AlertGlyph))
        {
            OnPropertyChanged(nameof(WeatherAlertGlyph));
        }

        // 换地方或换数据源，立刻重新抓一次，别等下一个周期
        if (e.PropertyName is nameof(WeatherConfig.Latitude) or nameof(WeatherConfig.Longitude)
            or nameof(WeatherConfig.Provider))
        {
            _ = _monitor.RefreshNowAsync();
        }
    }

    /// <summary>按当前配置决定用哪种图标画法。</summary>
    private void ApplyIconMode()
    {
        ShowGlyph = Config.IconMode == WeatherIconMode.SystemGlyph;
        ShowEmoji = Config.IconMode == WeatherIconMode.Emoji;
        ShowImage = Config.IconMode == WeatherIconMode.CustomImage;
        IconFont = new FontFamily(Config.EmojiFontFamily);
        IconImageHeight = Config.CustomImageHeight;
    }

    private void Apply(WeatherSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        ConditionText = WeatherText.Describe(snapshot.Condition);
        TemperatureText = Format(snapshot.Temperature, "°");
        FeelsLikeText = "体感 " + Format(snapshot.FeelsLike, "°");
        HumidityText = "湿度 " + Format(snapshot.Humidity, "%");
        WindText = snapshot.WindDirection < 0
            ? Format(snapshot.WindSpeed, " km/h")
            : WeatherText.Direction(snapshot.WindDirection) + "风 " + Format(snapshot.WindSpeed, " km/h");
        TodayRangeText = snapshot.HasDailyRange
            ? Format(snapshot.TodayMin, "°") + " / " + Format(snapshot.TodayMax, "°")
            : string.Empty;

        IconGlyph = WeatherIconCatalog.Glyph(snapshot.Condition, snapshot.IsDay);
        IconEmoji = WeatherIconCatalog.Emoji(snapshot.Condition, snapshot.IsDay);
        LoadCustomImage(snapshot.Condition, snapshot.IsDay);
    }

    /// <summary>自定义图片模式下把图读进来，按路径缓存，别每轮都重新解码。</summary>
    private void LoadCustomImage(WeatherCondition condition, bool isDay)
    {
        if (Config.IconMode != WeatherIconMode.CustomImage)
        {
            return;
        }

        var path = WeatherIconCatalog.ResolveImagePath(Config.CustomImageFolder, condition, isDay);
        if (string.IsNullOrEmpty(path))
        {
            IconImage = null;
            _cachedImagePath = null;
            _cachedImage = null;
            return;
        }

        if (path == _cachedImagePath && _cachedImage != null)
        {
            IconImage = _cachedImage;
            return;
        }

        try
        {
            var bitmap = new Bitmap(path);
            _cachedImagePath = path;
            _cachedImage = bitmap;
            IconImage = bitmap;
        }
        catch (Exception)
        {
            // 图坏了就当没有，不要因为一张图标把组件搞崩
            IconImage = null;
            _cachedImagePath = null;
            _cachedImage = null;
        }
    }

    private static string Format(double value, string unit) =>
        value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
}
