// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.ComponentModel;
using Avalonia.Controls;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.ViewModels;

namespace ClassIsland.Promethium.Views.SettingsPages;

/// <summary>
/// 设置窗口里「Pm优化 → 更好的天气」这一页。
/// </summary>
/// <remarks>
/// 位置配置放在这里而不是组件设置里，和宿主「天气」页的分工保持一致：
/// 在哪儿看是全局的，看什么由各自的组件决定。
/// </remarks>
[SettingsPageInfo("qiuxiyueshijiu.promethium.better-weather", "更好的天气", "\uF44F", "\uF44E")]
[Group(PromethiumPlugin.SettingsGroupId)]
public partial class BetterWeatherSettingsPage : SettingsPageBase
{
    /// <summary>页面的视图模型。</summary>
    public BetterWeatherSettingsViewModel ViewModel { get; }

    public BetterWeatherSettingsPage(PromethiumConfigStore store, NominatimService geocoder)
    {
        ViewModel = new BetterWeatherSettingsViewModel(store, geocoder);

        InitializeComponent();
        Root.DataContext = ViewModel;

        Map.SetView(ViewModel.Config.Latitude, ViewModel.Config.Longitude, 15);
        Map.LocationPicked += async (_, e) => await ViewModel.PickAsync(e.Latitude, e.Longitude);

        CandidateList.SelectionChanged += async (_, _) =>
        {
            if (CandidateList.SelectedItem is PlaceCandidate candidate)
            {
                CandidateList.SelectedItem = null;
                await ViewModel.ChooseAsync(candidate);
            }
        };

        // 直接改经纬度时地图跟着走，不然数字和地图会对不上
        ViewModel.Config.PropertyChanged += OnConfigChanged;
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BetterWeatherConfig.Latitude) or nameof(BetterWeatherConfig.Longitude))
        {
            Map.SetView(ViewModel.Config.Latitude, ViewModel.Config.Longitude);
        }
    }
}
