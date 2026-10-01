// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Models;

/// <summary>
/// 地震速报的配置。
/// </summary>
/// <remarks>
/// <b>关于「不要谎报军情」</b>：这里的数据来自 USGS 公开地震目录，是真实观测结果，
/// 不是推算出来的。但它是「事后速报」——地震已经发生、目录更新之后才拿得到，
/// 提供不了震前预警。所以界面上和文案里都不允许出现「震前预警」这类说法，
/// 也默认关掉，由用户自己决定要不要开。
/// </remarks>
public partial class EarthquakeConfig : ObservableObject
{
    /// <summary>是否开启地震速报。默认关闭：这个功能应该由用户主动开。</summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>数据来源。</summary>
    [ObservableProperty]
    private EarthquakeSource _source = EarthquakeSource.Usgs;

    /// <summary>关注半径（公里）：震中在这个范围内才提醒。</summary>
    [ObservableProperty]
    private double _radiusKm = 300d;

    /// <summary>最低震级：小于这个震级不提醒。</summary>
    [ObservableProperty]
    private double _minMagnitude = 4d;

    /// <summary>回看多少小时内的地震。</summary>
    [ObservableProperty]
    private double _lookbackHours = 24d;

    /// <summary>轮询间隔（分钟）。</summary>
    [ObservableProperty]
    private double _pollIntervalMinutes = 5d;

    /// <summary>报警送到哪儿。</summary>
    [ObservableProperty]
    private AlertDelivery _delivery = AlertDelivery.Both;

    /// <summary>报警文案模板，支持 {变量}。</summary>
    [ObservableProperty]
    private string _notifyTemplate =
        "【地震速报】{时间} {地点} 发生 {震级} 级地震，震源深度 {深度} 公里，距你约 {距离} 公里。";

    /// <summary>组件上实时显示的文案模板，支持 {变量}。</summary>
    [ObservableProperty]
    private string _inlineTemplate = "地震 {震级} 级 · {距离} 公里 · {地点}";

    /// <summary>可选的自定义提示音文件路径。</summary>
    [ObservableProperty]
    private string _soundPath = string.Empty;

    /// <summary>自定义提示音的音量（0~1）。</summary>
    [ObservableProperty]
    private double _soundVolume = 1d;

    /// <summary>
    /// 已经提醒过的事件编号。存盘是为了让插件重启后不会把同一次地震再报一遍。
    /// </summary>
    public List<string> NotifiedEventIds { get; set; } = new();
}
