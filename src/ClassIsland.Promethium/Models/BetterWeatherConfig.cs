// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Models;

/// <summary>
/// 「更好的天气」的全局配置：看哪个点。
/// </summary>
/// <remarks>
/// 位置放在这里而不是组件设置里，是照着宿主的分工来的——
/// 宿主也是「城市在设置窗口的天气页、显示什么在组件的设置里」。
/// 这样加两个天气组件不会各自记一个城市，人也不会找不着地方改。
/// </remarks>
public partial class BetterWeatherConfig : ObservableObject
{
    /// <summary>纬度。</summary>
    [ObservableProperty]
    private double _latitude = 39.9042;

    /// <summary>经度。</summary>
    [ObservableProperty]
    private double _longitude = 116.4074;

    /// <summary>地点显示名，由逆地理编码反查到街道。</summary>
    [ObservableProperty]
    private string _locationName = "北京市东城区";

    /// <summary>用户是否已经动手选过位置。没选过就显示默认点并给出提示。</summary>
    [ObservableProperty]
    private bool _isLocationPicked;
}
