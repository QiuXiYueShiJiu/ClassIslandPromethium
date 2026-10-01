// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Models;

/// <summary>
/// 「更好的天气」组件自己的设置，只管显示什么。
/// </summary>
/// <remarks>
/// 看哪个点、用哪个数据源、图标怎么画，属于全局配置，在 <see cref="WeatherConfig"/> 里。
/// </remarks>
public partial class BetterWeatherSettings : ObservableRecipient
{
    /// <summary>显示模式。不是「自定义」时，下面那些逐项开关不起作用。</summary>
    [ObservableProperty]
    private WeatherDisplayMode _displayMode = WeatherDisplayMode.Standard;

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
    /// 给下拉框用的整数视图。
    /// </summary>
    /// <remarks>
    /// ComboBox 的 SelectedIndex 是 int，直接绑枚举要指望绑定引擎替我们做转换，
    /// 转换失败又不会报错、只会静默不生效。隔一层显式映射就不存在这个问题。
    /// 这个属性只是给界面看的，不进配置文件。
    /// </remarks>
    [JsonIgnore]
    public int DisplayModeIndex
    {
        get => (int)DisplayMode;
        set
        {
            if (value >= 0 && value <= (int)WeatherDisplayMode.Custom)
            {
                DisplayMode = (WeatherDisplayMode)value;
            }
        }
    }
}
