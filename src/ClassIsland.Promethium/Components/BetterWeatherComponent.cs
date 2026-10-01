// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.ViewModels;

namespace ClassIsland.Promethium.Components;

/// <summary>
/// 「更好的天气」主界面组件。
/// </summary>
/// <remarks>
/// 和原版天气简报的分工：原版按城市查、插件拿不到它的城市表；
/// 这里按自己记住的经纬度直接查，所以能精确到你在地图上点的那一个点，
/// 并且把反查出来的街道名一起显示出来。
/// </remarks>
[ComponentInfo("cdce0a20-21bf-4586-8054-aba3c5308924", "更好的天气", "\uF465",
    "显示指定经纬度（可精确到街道）的天气，设置页支持地图选点。")]
public partial class BetterWeatherComponent : ComponentBase<BetterWeatherSettings>
{
    private readonly WeatherMonitor _monitor;

    /// <summary>显示用的视图模型。</summary>
    public BetterWeatherViewModel ViewModel { get; }

    public BetterWeatherComponent(WeatherMonitor monitor, PromethiumConfigStore configStore, AlertCenter alerts)
    {
        _monitor = monitor;
        ViewModel = new BetterWeatherViewModel(monitor, configStore, alerts);
        InitializeComponent();

        // 挂在内部元素上而不是组件本身：宿主可能也会动组件的 DataContext，
        // 挂在里面就不会被它覆盖掉。
        Root.DataContext = ViewModel;

        AttachedToVisualTree += OnAttached;
    }

    private void OnAttached(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // 设置是宿主构造之后才注入的，到挂上视觉树时肯定已经有了。
        if (Settings != null)
        {
            ViewModel.AttachSettings(Settings);
        }

        // 监测器会自己按间隔刷新；这里只在还没有数据时催一下，避免开天窗
        if (_monitor.Snapshot == null)
        {
            _ = ViewModel.RefreshAsync();
        }
    }
}
