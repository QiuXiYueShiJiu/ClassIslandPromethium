// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Views.ComponentSettings;

/// <summary>
/// 「更好的天气」的组件设置界面（右键主界面组件 → 设置时弹出）。
/// </summary>
/// <remarks>
/// 这里刻意只放显示相关的开关；选位置那套东西放在设置窗口的页里，
/// 免得同一个功能有两个入口、两边状态还对不上。
/// </remarks>
public partial class BetterWeatherComponentSettingsControl : ComponentBase<BetterWeatherSettings>
{
    public BetterWeatherComponentSettingsControl()
    {
        InitializeComponent();
    }
}
