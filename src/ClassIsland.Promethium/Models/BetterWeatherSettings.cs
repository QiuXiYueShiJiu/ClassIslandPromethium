// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Models;

/// <summary>
/// 「更好的天气」组件自己的设置，只管显示什么。
/// </summary>
/// <remarks>
/// 看哪个点属于全局配置，在 <see cref="BetterWeatherConfig"/> 里。
/// </remarks>
public partial class BetterWeatherSettings : ObservableRecipient
{
    /// <summary>在组件上显示街道级位置名。</summary>
    [ObservableProperty]
    private bool _showLocationName = true;

    /// <summary>显示今日最低/最高温。</summary>
    [ObservableProperty]
    private bool _showDailyRange = true;

    /// <summary>显示湿度。</summary>
    [ObservableProperty]
    private bool _showHumidity = true;

    /// <summary>显示风力。</summary>
    [ObservableProperty]
    private bool _showWind;

    /// <summary>显示体感温度。</summary>
    [ObservableProperty]
    private bool _showFeelsLike;

    /// <summary>
    /// 天气数据刷新间隔（分钟），小于等于 0 表示不自动刷新。
    /// 用 double 是为了直接和 NumberBox 对上，少一层隐式转换。
    /// </summary>
    [ObservableProperty]
    private double _refreshIntervalMinutes = 15d;
}
