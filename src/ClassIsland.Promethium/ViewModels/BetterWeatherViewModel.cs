// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.ComponentModel;
using System.Globalization;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.ViewModels;

/// <summary>
/// 「更好的天气」组件显示用的视图模型。
/// </summary>
/// <remarks>
/// 界面上一律显示已经格式化好的字符串，省掉一堆 XAML 转换器——
/// 少一层转换就少一处出错的地方。
/// </remarks>
public partial class BetterWeatherViewModel : ObservableObject
{
    private readonly OpenMeteoService _weather;
    private BetterWeatherSettings? _settings;
    private bool _isRefreshing;

    /// <summary>看哪个点，全局配置。</summary>
    public BetterWeatherConfig Config { get; }

    /// <summary>组件自己的显示设置。宿主构造完之后才注入，所以可能为 null。</summary>
    public BetterWeatherSettings? Settings => _settings;

    /// <summary>天气图标字形。</summary>
    [ObservableProperty]
    private string _glyph = "\uF80F4";

    /// <summary>天气现象文字。</summary>
    [ObservableProperty]
    private string _weatherText = "等待数据";

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

    /// <summary>提示 / 错误信息，空串表示不显示。</summary>
    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>是否正在请求数据。</summary>
    [ObservableProperty]
    private bool _isLoading;

    public BetterWeatherViewModel(OpenMeteoService weather, PromethiumConfigStore store)
    {
        _weather = weather;
        Config = store.BetterWeather;
        Config.PropertyChanged += OnConfigChanged;
    }

    /// <summary>
    /// 关联组件设置。宿主在构造之后才注入设置，所以这个要能被反复调用。
    /// </summary>
    public void AttachSettings(BetterWeatherSettings settings)
    {
        if (ReferenceEquals(_settings, settings))
        {
            return;
        }

        if (_settings != null)
        {
            _settings.PropertyChanged -= OnSettingsChanged;
        }

        _settings = settings;
        _settings.PropertyChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 改刷新间隔就立刻按新间隔走一次，不用等下一个周期
        if (e.PropertyName == nameof(BetterWeatherSettings.RefreshIntervalMinutes))
        {
            RefreshIntervalChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 只有换地方才需要重新查天气，改个地名不用动网络
        if (e.PropertyName is nameof(BetterWeatherConfig.Latitude)
            or nameof(BetterWeatherConfig.Longitude))
        {
            _ = RefreshAsync();
        }
    }

    /// <summary>刷新间隔变了，通知组件重建计时器。</summary>
    public event EventHandler? RefreshIntervalChanged;

    /// <summary>拉一次天气数据并刷新界面文字。</summary>
    public async Task RefreshAsync(CancellationToken token = default)
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        IsLoading = true;
        StatusText = string.Empty;
        try
        {
            var snapshot = await _weather.QueryAsync(Config.Latitude, Config.Longitude, token)
                .ConfigureAwait(true);
            Apply(snapshot);
        }
        catch (OperationCanceledException)
        {
            // 主动取消不算错误
        }
        catch (Exception ex)
        {
            StatusText = "天气获取失败";
            WeatherText = ex.GetType().Name;
        }
        finally
        {
            _isRefreshing = false;
            IsLoading = false;
        }
    }

    private void Apply(WeatherSnapshot snapshot)
    {
        Glyph = WmoWeatherCode.Glyph(snapshot.WeatherCode, snapshot.IsDay);
        WeatherText = WmoWeatherCode.Describe(snapshot.WeatherCode);
        TemperatureText = Format(snapshot.Temperature, "°");
        FeelsLikeText = "体感 " + Format(snapshot.FeelsLike, "°");
        HumidityText = "湿度 " + Format(snapshot.Humidity, "%");
        WindText = WmoWeatherCode.Direction(snapshot.WindDirection) + "风 "
                   + Format(snapshot.WindSpeed, " km/h");
        TodayRangeText = Format(snapshot.TodayMin, "°") + " / " + Format(snapshot.TodayMax, "°");
    }

    private static string Format(double value, string unit) =>
        value.ToString("0.#", CultureInfo.InvariantCulture) + unit;
}
