// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Attributes;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.ViewModels;

namespace ClassIsland.Promethium.Views.SettingsPages;

/// <summary>
/// 设置窗口里「Pm优化 → 更好的天气」这一页。
/// </summary>
/// <remarks>
/// 位置单独一张卡片放在设置块外面，因为它是这个页面里最常改的东西，
/// 埋在折叠项里不好找。
/// </remarks>
[SettingsPageInfo("qiuxiyueshijiu.promethium.better-weather", "更好的天气", "\uF44F", "\uF44E")]
[Group(PromethiumPlugin.SettingsGroupId)]
public partial class BetterWeatherSettingsPage : SettingsPageBase
{
    /// <summary>页面的视图模型。</summary>
    public BetterWeatherSettingsViewModel ViewModel { get; }

    public BetterWeatherSettingsPage(
        PromethiumConfigStore store,
        NominatimService geocoder,
        WeatherProviderCatalog weatherCatalog,
        IAudioService audioService)
    {
        ViewModel = new BetterWeatherSettingsViewModel(store, geocoder, weatherCatalog, audioService);

        InitializeComponent();
        Root.DataContext = ViewModel;
        ViewModel.NotifySelectionRefresh();

        Map.SetView(ViewModel.Weather.Latitude, ViewModel.Weather.Longitude, 15);
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
        ViewModel.Weather.PropertyChanged += OnWeatherConfigChanged;
    }

    private void OnWeatherConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WeatherConfig.Latitude) or nameof(WeatherConfig.Longitude))
        {
            Map.SetView(ViewModel.Weather.Latitude, ViewModel.Weather.Longitude);
        }
    }

    private void OnBrowseSound(object? sender, RoutedEventArgs e) =>
        _ = BrowseFileAsync(path => ViewModel.Weather.SoundPath = path);

    private void OnBrowseImageFolder(object? sender, RoutedEventArgs e) => _ = BrowseFolderAsync();

    private async Task BrowseFileAsync(Action<string> apply)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择提示音",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("音频文件")
                {
                    Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.flac", "*.m4a", "*.aac" }
                }
            }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrEmpty(path))
        {
            apply(path);
        }
    }

    private async Task BrowseFolderAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择存放天气图标的目录",
            AllowMultiple = false
        });

        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.Weather.CustomImageFolder = path;
        }
    }
}
