// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Collections.ObjectModel;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClassIsland.Promethium.ViewModels;

/// <summary>
/// 设置窗口里「Pm优化 → 更好的天气」这一页的视图模型。
/// </summary>
/// <remarks>
/// 位置、数据源、图标、报警全都在这一个页面里改，分两个块：
/// 天气一块、地震速报一块。落盘由配置存储统一负责，这里不自己写文件。
/// </remarks>
public partial class BetterWeatherSettingsViewModel : ObservableObject
{
    private readonly NominatimService _geocoder;
    private readonly WeatherProviderCatalog _weatherCatalog;
    private readonly EarthquakeProviderCatalog _earthquakeCatalog;
    private readonly EarthquakeMonitor _earthquakeMonitor;
    private readonly IAudioService _audioService;

    /// <summary>天气那一块的配置。</summary>
    public WeatherConfig Weather { get; }

    /// <summary>地震速报那一块的配置。</summary>
    public EarthquakeConfig Earthquake { get; }

    /// <summary>可选天气数据源。</summary>
    public IReadOnlyList<WeatherProviderInfo> WeatherProviders => _weatherCatalog.All;

    /// <summary>可选地震目录。</summary>
    public IReadOnlyList<EarthquakeProviderInfo> EarthquakeProviders => _earthquakeCatalog.All;

    /// <summary>可按地名搜出来的候选点。</summary>
    public ObservableCollection<PlaceCandidate> Candidates { get; } = new();

    /// <summary>天气块里可以用的变量说明。</summary>
    public string WeatherVariableHint { get; } =
        "可用变量：" + TemplateEngine.Describe(AlertTextComposer.WeatherVariables);

    /// <summary>地震块里可以用的变量说明。</summary>
    public string EarthquakeVariableHint { get; } =
        "可用变量：" + TemplateEngine.Describe(AlertTextComposer.EarthquakeVariables);

    /// <summary>自定义图片模式的文件命名约定。</summary>
    public string ImageNamingHint { get; } =
        "按文件名取图：" + WeatherIconCatalog.DescribeNamingConvention()
        + "；夜间版本在名字后加 _night，例如 clear_night.png。";

    /// <summary>搜索框里的字。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>天气块的状态提示。</summary>
    [ObservableProperty]
    private string _statusText = "在地图上点一下，或者搜个地名。";

    /// <summary>地震块的状态提示。</summary>
    [ObservableProperty]
    private string _earthquakeStatusText = "开启后每隔一段时间检查一次公开地震目录。";

    /// <summary>正在联网。</summary>
    [ObservableProperty]
    private bool _isBusy;

    public BetterWeatherSettingsViewModel(
        PromethiumConfigStore store,
        NominatimService geocoder,
        WeatherProviderCatalog weatherCatalog,
        EarthquakeProviderCatalog earthquakeCatalog,
        EarthquakeMonitor earthquakeMonitor,
        IAudioService audioService)
    {
        _geocoder = geocoder;
        _weatherCatalog = weatherCatalog;
        _earthquakeCatalog = earthquakeCatalog;
        _earthquakeMonitor = earthquakeMonitor;
        _audioService = audioService;

        Weather = store.Weather;
        Earthquake = store.Earthquake;

        // 图标方式一变，几个可见性开关要跟着变，不然设置页会显示错行
        Weather.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WeatherConfig.IconMode))
            {
                OnPropertyChanged(nameof(IsGlyphMode));
                OnPropertyChanged(nameof(IsEmojiMode));
                OnPropertyChanged(nameof(IsImageMode));
            }
        };

        _earthquakeMonitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EarthquakeMonitor.ErrorText))
            {
                EarthquakeStatusText = string.IsNullOrEmpty(_earthquakeMonitor.ErrorText)
                    ? "检查正常。"
                    : _earthquakeMonitor.ErrorText;
            }
        };
    }

    /// <summary>当前选中数据源的说明，显示在设置页里。</summary>
    public string SelectedWeatherProviderDescription =>
        WeatherProviders[SelectedWeatherProviderIndex].Description;

    /// <summary>当前选中地震目录的说明。</summary>
    public string SelectedEarthquakeProviderDescription =>
        EarthquakeProviders[SelectedEarthquakeProviderIndex].Description;

    /// <summary>当前是不是用系统字形。</summary>
    public bool IsGlyphMode => Weather.IconMode == WeatherIconMode.SystemGlyph;

    /// <summary>当前是不是用 emoji。</summary>
    public bool IsEmojiMode => Weather.IconMode == WeatherIconMode.Emoji;

    /// <summary>当前是不是用自定义图片。</summary>
    public bool IsImageMode => Weather.IconMode == WeatherIconMode.CustomImage;

    // ---------- 下拉框的索引映射 ----------
    // 用索引而不是直接绑枚举：ComboBox 的 SelectedIndex 是 int，
    // 中间隔一层显式映射就不会依赖绑定引擎去做枚举转换。

    /// <summary>当前天气数据源在列表里的位置。</summary>
    public int SelectedWeatherProviderIndex
    {
        get => IndexOf(WeatherProviders, i => i.Kind == Weather.Provider);
        set
        {
            if (value >= 0 && value < WeatherProviders.Count)
            {
                Weather.Provider = WeatherProviders[value].Kind;
                OnPropertyChanged(nameof(SelectedWeatherProviderDescription));
            }
        }
    }

    /// <summary>当前地震目录在列表里的位置。</summary>
    public int SelectedEarthquakeProviderIndex
    {
        get => IndexOf(EarthquakeProviders, i => i.Source == Earthquake.Source);
        set
        {
            if (value >= 0 && value < EarthquakeProviders.Count)
            {
                Earthquake.Source = EarthquakeProviders[value].Source;
                OnPropertyChanged(nameof(SelectedEarthquakeProviderDescription));
            }
        }
    }

    /// <summary>图标显示方式在枚举里的位置。</summary>
    public int SelectedIconModeIndex
    {
        get => (int)Weather.IconMode;
        set
        {
            if (value >= 0 && value <= (int)WeatherIconMode.TextOnly)
            {
                Weather.IconMode = (WeatherIconMode)value;
            }
        }
    }

    /// <summary>天气报警模式的索引。</summary>
    public int SelectedWeatherDeliveryIndex
    {
        get => (int)Weather.Delivery;
        set
        {
            if (value >= 0 && value <= (int)AlertDelivery.Both)
            {
                Weather.Delivery = (AlertDelivery)value;
            }
        }
    }

    /// <summary>地震速报报警模式的索引。</summary>
    public int SelectedEarthquakeDeliveryIndex
    {
        get => (int)Earthquake.Delivery;
        set
        {
            if (value >= 0 && value <= (int)AlertDelivery.Both)
            {
                Earthquake.Delivery = (AlertDelivery)value;
            }
        }
    }

    private static int IndexOf<T>(IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                return i;
            }
        }

        return 0;
    }

    // ---------- 位置 ----------

    /// <summary>确定选了这个坐标，顺手反查一下地名。</summary>
    public async Task PickAsync(double latitude, double longitude)
    {
        Weather.Latitude = latitude;
        Weather.Longitude = longitude;
        Weather.IsLocationPicked = true;
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
            var name = await _geocoder.ReverseAsync(Weather.Latitude, Weather.Longitude);
            if (!string.IsNullOrWhiteSpace(name))
            {
                Weather.LocationName = name;
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

            StatusText = results.Count > 0
                ? $"找到 {results.Count} 个候选点，选一个。"
                : "没搜到，换个说法试试（可以直接写「xx镇」「xx街道」「xx村」）。";
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

    // ---------- 提示音 ----------

    /// <summary>试听天气报警的提示音。</summary>
    [RelayCommand]
    private Task TestWeatherSoundAsync() => PlaySoundAsync(Weather.SoundPath, Weather.SoundVolume, "天气");

    /// <summary>试听地震速报的提示音。</summary>
    [RelayCommand]
    private Task TestEarthquakeSoundAsync() => PlaySoundAsync(Earthquake.SoundPath, Earthquake.SoundVolume, "地震");

    private async Task PlaySoundAsync(string path, double volume, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            StatusText = $"还没给{label}报警选音频文件。";
            return;
        }

        try
        {
            await _audioService.PlayAudioAsync(path, (float)Math.Clamp(volume, 0d, 1d), null);
        }
        catch (Exception ex)
        {
            StatusText = "播放失败：" + ex.GetType().Name;
        }
    }

    // ---------- 地震 ----------

    /// <summary>立刻检查一次地震目录。</summary>
    [RelayCommand]
    private async Task CheckEarthquakeAsync()
    {
        EarthquakeStatusText = "正在检查…";
        try
        {
            await _earthquakeMonitor.CheckNowAsync();
            EarthquakeStatusText = string.IsNullOrEmpty(_earthquakeMonitor.ErrorText)
                ? $"检查完成，本次共取回 {_earthquakeMonitor.LastQueriedCount} 条记录。"
                : _earthquakeMonitor.ErrorText;
        }
        catch (Exception ex)
        {
            EarthquakeStatusText = "检查失败：" + ex.GetType().Name;
        }
    }

    /// <summary>设置页被打开时刷新一下下拉框的选中项。</summary>
    public void NotifySelectionRefresh()
    {
        OnPropertyChanged(nameof(SelectedWeatherProviderIndex));
        OnPropertyChanged(nameof(SelectedEarthquakeProviderIndex));
        OnPropertyChanged(nameof(SelectedIconModeIndex));
        OnPropertyChanged(nameof(SelectedWeatherDeliveryIndex));
        OnPropertyChanged(nameof(SelectedEarthquakeDeliveryIndex));
    }
}
