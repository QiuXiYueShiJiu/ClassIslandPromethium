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
/// 位置、数据源、图标、报警都在这一页里改。落盘由配置存储统一负责，这里不自己写文件。
/// </remarks>
public partial class BetterWeatherSettingsViewModel : ObservableObject
{
    private readonly NominatimService _geocoder;
    private readonly WeatherProviderCatalog _weatherCatalog;
    private readonly IAudioService _audioService;

    /// <summary>天气配置。</summary>
    public WeatherConfig Weather { get; }

    /// <summary>可选天气数据源。</summary>
    public IReadOnlyList<WeatherProviderInfo> WeatherProviders => _weatherCatalog.All;

    /// <summary>可按地名搜出来的候选点。</summary>
    public ObservableCollection<PlaceCandidate> Candidates { get; } = new();

    /// <summary>报警文案里可以用的变量说明。</summary>
    public string WeatherVariableHint { get; } =
        "可用变量：" + TemplateEngine.Describe(AlertTextComposer.WeatherVariables);

    /// <summary>自定义图片模式的文件命名约定。</summary>
    public string ImageNamingHint { get; } =
        "按文件名取图：" + WeatherIconCatalog.DescribeNamingConvention()
        + "；夜间版本在名字后加 _night，例如 clear_night.png。";

    /// <summary>搜索框里的字。</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>状态提示。</summary>
    [ObservableProperty]
    private string _statusText = "在地图上点一下，或者搜个地名。";

    /// <summary>正在联网。</summary>
    [ObservableProperty]
    private bool _isBusy;

    public BetterWeatherSettingsViewModel(
        PromethiumConfigStore store,
        NominatimService geocoder,
        WeatherProviderCatalog weatherCatalog,
        IAudioService audioService)
    {
        _geocoder = geocoder;
        _weatherCatalog = weatherCatalog;
        _audioService = audioService;

        Weather = store.Weather;

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
    }

    /// <summary>当前选中数据源那条台账记录。</summary>
    private WeatherProviderInfo SelectedInfo =>
        WeatherProviders.FirstOrDefault(i => i.Kind == Weather.Provider) ?? WeatherProviders[0];

    /// <summary>当前选中数据源的说明，显示在设置页里。</summary>
    public string SelectedWeatherProviderDescription => SelectedInfo.Description;

    /// <summary>当前数据源的覆盖范围。</summary>
    public string SelectedProviderRegion => SelectedInfo.Region;

    /// <summary>当前数据源要不要密钥。</summary>
    public bool SelectedProviderRequiresKey => SelectedInfo.RequiresApiKey;

    /// <summary>当前数据源去哪申请密钥；免密钥的为空串。</summary>
    public string SelectedProviderApiKeyUrl => SelectedInfo.ApiKeyUrl;

    /// <summary>密钥输入框的提示语。</summary>
    public string ApiKeyHint =>
        SelectedProviderRequiresKey
            ? $"该数据源需要密钥。去 {SelectedProviderApiKeyUrl} 申请后粘到这里，密钥按数据源分别保存，来回切换不用重填。"
            : "当前数据源不需要密钥。";

    /// <summary>
    /// 当前数据源的密钥。
    /// </summary>
    /// <remarks>
    /// 读写都落到配置里按数据源分开的那份字典上，所以几个源之间来回切不会互相覆盖。
    /// </remarks>
    public string ApiKey
    {
        get => Weather.GetApiKey(Weather.Provider);
        set => Weather.SetApiKey(Weather.Provider, value ?? string.Empty);
    }

    /// <summary>当前是不是用系统字形。</summary>
    public bool IsGlyphMode => Weather.IconMode == WeatherIconMode.SystemGlyph;

    /// <summary>当前是不是用 emoji。</summary>
    public bool IsEmojiMode => Weather.IconMode == WeatherIconMode.Emoji;

    /// <summary>当前是不是用自定义图片。</summary>
    public bool IsImageMode => Weather.IconMode == WeatherIconMode.CustomImage;

    // ---------- 下拉框的索引映射 ----------
    // 用索引而不是直接绑枚举：ComboBox 的 SelectedIndex 是 int，
    // 中间隔一层显式映射就不会依赖绑定引擎去做枚举转换（转换失败还不报错）。

    /// <summary>
    /// 当前选中的数据源。
    /// </summary>
    /// <remarks>
    /// 故意绑对象而不是绑索引：绑索引的话，台账列表的顺序一旦和枚举顺序对不上，
    /// 下拉框就会选到错的那个源，而且不会报任何错。绑对象就不存在这个隐患。
    /// </remarks>
    public WeatherProviderInfo? SelectedWeatherProvider
    {
        get => WeatherProviders.FirstOrDefault(i => i.Kind == Weather.Provider);
        set
        {
            if (value == null || value.Kind == Weather.Provider)
            {
                return;
            }

            Weather.Provider = value.Kind;
            OnPropertyChanged(nameof(SelectedWeatherProviderDescription));
            OnPropertyChanged(nameof(SelectedProviderRegion));
            OnPropertyChanged(nameof(SelectedProviderRequiresKey));
            OnPropertyChanged(nameof(SelectedProviderApiKeyUrl));
            OnPropertyChanged(nameof(ApiKeyHint));
            // 换源了，输入框要显示这个源自己的密钥
            OnPropertyChanged(nameof(ApiKey));
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

    /// <summary>报警模式的索引。</summary>
    public int SelectedDeliveryIndex
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

    /// <summary>试听报警提示音。</summary>
    [RelayCommand]
    private async Task TestSoundAsync()
    {
        if (string.IsNullOrWhiteSpace(Weather.SoundPath))
        {
            StatusText = "还没给报警选音频文件。";
            return;
        }

        try
        {
            await _audioService.PlayAudioAsync(Weather.SoundPath,
                (float)Math.Clamp(Weather.SoundVolume, 0d, 1d), null);
        }
        catch (Exception ex)
        {
            StatusText = "播放失败：" + ex.GetType().Name;
        }
    }

    /// <summary>设置页被打开时刷新一下下拉框的选中项。</summary>
    public void NotifySelectionRefresh()
    {
        OnPropertyChanged(nameof(SelectedWeatherProvider));
        OnPropertyChanged(nameof(SelectedIconModeIndex));
        OnPropertyChanged(nameof(SelectedDeliveryIndex));
    }
}
