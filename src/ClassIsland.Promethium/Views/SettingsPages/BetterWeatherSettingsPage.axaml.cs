// Pm钷 —— ClassIsland 综合增强插件
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
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
        GeocodingService geocoder,
        WeatherProviderCatalog weatherCatalog,
        IAudioService audioService,
        AutoConfigurator autoConfigurator,
        WeatherMonitor monitor)
    {
        ViewModel = new BetterWeatherSettingsViewModel(store, geocoder, weatherCatalog, audioService, autoConfigurator, monitor);

        InitializeComponent();
        Root.DataContext = ViewModel;
        ViewModel.NotifySelectionRefresh();

        Map.SetView(ViewModel.Weather.Latitude, ViewModel.Weather.Longitude, 15);
        Map.LocationPicked += async (_, e) => await ViewModel.PickAsync(e.Latitude, e.Longitude);

        // 关键一步：把配置里的底图候选交给地图控件。
        // 之前漏了这句，导致底图规则一直是空的，地图上只画得出网格和标记。
        ApplyTileSource();
        Map.TileStatusChanged += (_, _) => UpdateTileStatusText();
        // 自动配置可能改了底图，改完要重新把候选交给地图控件
        ViewModel.TileSourceChanged += (_, _) => ApplyTileSource();

        CandidateList.SelectionChanged += async (_, _) =>
        {
            if (CandidateList.SelectedItem is PlaceCandidate candidate)
            {
                CandidateList.SelectedItem = null;
                await ViewModel.ChooseAsync(candidate);
            }
        };

        // 直接改经纬度时地图跟着走，不然数字和地图会对不上；
        // 改底图相关设置时立刻换源，不用等重开页面。
        ViewModel.Weather.PropertyChanged += OnWeatherConfigChanged;
    }

    /// <summary>
    /// 配置变了就同步界面。
    /// </summary>
    /// <remarks>
    /// <b>这里必须 marshal 回 UI 线程。</b>配置对象可能被后台任务改动
    /// （比如监测器、或者将来别的异步流程），而 Avalonia 控件只能从 UI 线程碰。
    /// 之前就是因为在后台线程上直接 InvalidateVisual 导致点「自动配置」后界面卡死。
    /// </remarks>
    private void OnWeatherConfigChanged(object? sender, PropertyChangedEventArgs e)
    {
        var touchesMap = e.PropertyName is nameof(WeatherConfig.Latitude) or nameof(WeatherConfig.Longitude);
        var touchesTiles = e.PropertyName is nameof(WeatherConfig.MapTileSource) or nameof(WeatherConfig.CustomTileUrl)
            or nameof(WeatherConfig.CustomTileDatum) or nameof(WeatherConfig.CustomTileAttribution);

        if (!touchesMap && !touchesTiles)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            SyncMap(touchesMap, touchesTiles);
        }
        else
        {
            Dispatcher.UIThread.Post(() => SyncMap(touchesMap, touchesTiles));
        }
    }

    private void SyncMap(bool touchesMap, bool touchesTiles)
    {
        if (touchesMap)
        {
            Map.SetView(ViewModel.Weather.Latitude, ViewModel.Weather.Longitude);
        }

        if (touchesTiles)
        {
            ApplyTileSource();
        }
    }

    /// <summary>把当前配置解析成底图候选交给地图控件，并刷新署名与状态。</summary>
    private void ApplyTileSource()
    {
        Map.Configure(MapTileCatalog.ResolveChain(ViewModel.Weather), string.Empty);
        AttributionText.Text = Map.Attribution;
        UpdateTileStatusText();
    }

    private void UpdateTileStatusText()
    {
        var status = Map.DescribeTileStatus();
        if (!string.IsNullOrEmpty(status))
        {
            ViewModel.StatusText = status;
        }
    }

    private void OnZoomIn(object? sender, RoutedEventArgs e) => Map.ZoomIn();

    private void OnZoomOut(object? sender, RoutedEventArgs e) => Map.ZoomOut();

    private void OnResetView(object? sender, RoutedEventArgs e) => Map.ResetView();

    private void OnReloadTiles(object? sender, RoutedEventArgs e)
    {
        Map.ReloadTiles();
        ViewModel.StatusText = "已重新加载底图。";
    }

    private void OnOpenApiKeyPage(object? sender, RoutedEventArgs e) => _ = OpenApiKeyPageAsync();

    private async Task OpenApiKeyPageAsync()
    {
        var url = ViewModel.SelectedProviderApiKeyUrl;
        var top = TopLevel.GetTopLevel(this);
        if (string.IsNullOrWhiteSpace(url) || top?.Launcher is not { } launcher)
        {
            return;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await launcher.LaunchUriAsync(uri);
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
