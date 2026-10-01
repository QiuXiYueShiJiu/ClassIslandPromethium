// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Collections.ObjectModel;
using System.ComponentModel;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassIsland.Promethium.ViewModels;

/// <summary>
/// 设置窗口里「更好的天气」那一页的视图模型。
/// </summary>
public partial class BetterWeatherSettingsViewModel : ObservableObject
{
    private readonly PromethiumConfigStore _store;
    private readonly NominatimService _geocoder;

    /// <summary>看哪个点，改它就是了。</summary>
    public BetterWeatherConfig Config => _store.BetterWeather;

    /// <summary>按地名搜出来的候选点。</summary>
    public ObservableCollection<PlaceCandidate> Candidates { get; } = new();

    /// <summary>搜索框里的字。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>给用户看的当前状态。</summary>
    [ObservableProperty]
    private string _statusText = "在地图上点一下，或者搜个地名。";

    /// <summary>正在联网。</summary>
    [ObservableProperty]
    private bool _isBusy;

    public BetterWeatherSettingsViewModel(PromethiumConfigStore store, NominatimService geocoder)
    {
        _store = store;
        _geocoder = geocoder;

        // 任何改动都落盘。配置文件很小，不值得为省这点 IO 引入脏标记。
        Config.PropertyChanged += OnConfigChanged;
    }

    private void OnConfigChanged(object? sender, PropertyChangedEventArgs e) => _store.Save();

    /// <summary>确定选了这个坐标，顺手反查一下地名。</summary>
    public async Task PickAsync(double latitude, double longitude)
    {
        Config.Latitude = latitude;
        Config.Longitude = longitude;
        Config.IsLocationPicked = true;
        await LookupNameAsync();
    }

    /// <summary>按当前坐标反查街道级地名。</summary>
    [RelayCommand]
    private async Task LookupNameAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在反查地名…";
        try
        {
            var name = await _geocoder.ReverseAsync(Config.Latitude, Config.Longitude);
            if (!string.IsNullOrWhiteSpace(name))
            {
                Config.LocationName = name;
                StatusText = "已定位到 " + name;
            }
            else
            {
                StatusText = "这个点附近查不到地名，坐标已经记下了。";
            }
        }
        catch (Exception ex)
        {
            StatusText = "反查地名失败：" + ex.GetType().Name;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>按地名搜候选点。</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy || string.IsNullOrWhiteSpace(SearchText))
        {
            return;
        }

        IsBusy = true;
        StatusText = "正在搜索…";
        try
        {
            var results = await _geocoder.SearchAsync(SearchText);
            Candidates.Clear();
            foreach (var candidate in results)
            {
                Candidates.Add(candidate);
            }
            StatusText = results.Count > 0 ? $"找到 {results.Count} 个候选点，选一个。" : "没搜到，换个说法试试。";
        }
        catch (Exception ex)
        {
            StatusText = "搜索失败：" + ex.GetType().Name;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>选了某个候选点。</summary>
    public async Task ChooseAsync(PlaceCandidate candidate)
    {
        Candidates.Clear();
        await PickAsync(candidate.Latitude, candidate.Longitude);
    }
}
