// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using Avalonia.Threading;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.WeatherProviders;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 天气的抓取与报警判定，整个插件里只有这一处在拉天气。
/// </summary>
/// <remarks>
/// 让多个组件各自去请求接口是件蠢事：重复花钱、数值还可能不一致。
/// 这里统一抓一次，组件只负责画，报警只负责弹。
/// </remarks>
public partial class WeatherMonitor : ObservableObject, IHostedService
{
    private readonly WeatherProviderCatalog _catalog;
    private readonly PromethiumConfigStore _store;
    private readonly AlertCenter _alertCenter;

    /// <summary>用来把等待中的循环立刻叫醒，用于「刚改完设置马上重取」。</summary>
    private readonly SemaphoreSlim _wake = new(0, 1);

    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    /// <summary>最近一次成功拿到的天气。失败时保留上一次的值，不清空。</summary>
    [ObservableProperty]
    private WeatherSnapshot? _snapshot;

    /// <summary>最近一次抓取是否失败。</summary>
    [ObservableProperty]
    private string _errorText = string.Empty;

    /// <summary>新出现的天气报警。已经按「类型+日期」去过重。</summary>
    public event EventHandler<WeatherAlert>? AlertRaised;

    /// <summary>天气数据更新了。</summary>
    public event EventHandler? SnapshotUpdated;

    public WeatherMonitor(WeatherProviderCatalog catalog, PromethiumConfigStore store, AlertCenter alertCenter)
    {
        _catalog = catalog;
        _store = store;
        _alertCenter = alertCenter;

        // 改位置、换数据源、换密钥都该立刻重取一次，不该让用户干等一个周期。
        // 挂在监测器上而不是组件上：用户完全可能没往主界面放组件。
        _store.Weather.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(WeatherConfig.Latitude) or nameof(WeatherConfig.Longitude)
                or nameof(WeatherConfig.Provider) or nameof(WeatherConfig.ApiKeys))
            {
                RequestRefresh();
            }
        };
    }

    /// <summary>叫醒等待中的循环，让它马上再取一次。</summary>
    public void RequestRefresh()
    {
        try
        {
            _wake.Release();
        }
        catch (SemaphoreFullException)
        {
            // 已经有一次待处理的请求了，不用再加
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellation = new CancellationTokenSource();
        _loop = RunAsync(_cancellation.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cancellation == null)
        {
            return;
        }

        await _cancellation.CancelAsync().ConfigureAwait(false);
        if (_loop != null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }
        }
    }

    /// <summary>立刻抓一次，供外部（比如刚改完设置）调用。</summary>
    public Task RefreshNowAsync() => FetchOnceAsync(_cancellation?.Token ?? CancellationToken.None);

    private async Task RunAsync(CancellationToken token)
    {
        // 等宿主把界面拉起来再开始，别在启动最忙的时候抢网络
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(8), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await FetchOnceAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // 单轮失败不影响下一轮
            }

            var minutes = _store.Weather.RefreshIntervalMinutes;
            if (minutes <= 0)
            {
                // 用户关掉了自动刷新，那就等一次手动触发或者下次改配置
                await Task.Delay(TimeSpan.FromMinutes(1), token).ConfigureAwait(false);
                continue;
            }

            // 要么等到下一个周期，要么被 RequestRefresh 提前叫醒
            try
            {
                await Task.WhenAny(
                    Task.Delay(TimeSpan.FromMinutes(minutes), token),
                    _wake.WaitAsync(token)).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task FetchOnceAsync(CancellationToken token)
    {
        var config = _store.Weather;
        var provider = _catalog.Resolve(config.Provider);

        WeatherSnapshot snapshot;
        try
        {
            snapshot = await provider
                .QueryAsync(new WeatherQuery(config.Latitude, config.Longitude,
                    config.GetApiKey(config.Provider)), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await OnUiAsync(() => ErrorText = $"{provider.DisplayName} 取数失败：{ex.GetType().Name}").ConfigureAwait(false);
            return;
        }

        var alerts = WeatherAlertEvaluator.Evaluate(snapshot, config);

        await OnUiAsync(() =>
        {
            Snapshot = snapshot;
            ErrorText = string.Empty;
            SnapshotUpdated?.Invoke(this, EventArgs.Empty);
            PresentInlineAlert(snapshot, alerts);
            RaiseNewAlerts(snapshot, alerts);
        }).ConfigureAwait(false);
    }

    /// <summary>把当前报警套进实时显示模板，写进 AlertCenter 供组件显示。</summary>
    private void PresentInlineAlert(WeatherSnapshot snapshot, IReadOnlyList<WeatherAlert> alerts)
    {
        var config = _store.Weather;
        if (alerts.Count == 0 || !WantsInline(config.Delivery))
        {
            _alertCenter.WeatherAlertText = string.Empty;
            return;
        }

        var alert = alerts[0];
        var variables = AlertTextComposer.BuildWeatherVariables(snapshot, alert, config.LocationName);
        _alertCenter.WeatherAlertText = TemplateEngine.Render(config.InlineTemplate, variables);
    }

    /// <summary>把这一天还没报过的报警抛出去。</summary>
    private void RaiseNewAlerts(WeatherSnapshot snapshot, IReadOnlyList<WeatherAlert> alerts)
    {
        var config = _store.Weather;
        if (alerts.Count == 0 || !WantsNotify(config.Delivery))
        {
            return;
        }

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        PruneOldKeys(config, today);

        foreach (var alert in alerts)
        {
            var key = alert.Key + "|" + today;
            if (config.NotifiedAlertKeys.Contains(key))
            {
                continue;
            }

            config.NotifiedAlertKeys.Add(key);
            AlertRaised?.Invoke(this, alert);
        }
    }

    /// <summary>把不属于今天的去重记录清掉，免得配置文件越存越长。</summary>
    private static void PruneOldKeys(WeatherConfig config, string today) =>
        config.NotifiedAlertKeys.RemoveAll(k => !k.EndsWith("|" + today, StringComparison.Ordinal));

    /// <summary>报警要不要弹提醒。</summary>
    public static bool WantsNotify(AlertDelivery delivery) =>
        delivery is AlertDelivery.Notify or AlertDelivery.Both;

    /// <summary>报警要不要在组件上实时显示。</summary>
    public static bool WantsInline(AlertDelivery delivery) =>
        delivery is AlertDelivery.Inline or AlertDelivery.Both;

    private static Task OnUiAsync(Action action) =>
        Dispatcher.UIThread.CheckAccess()
            ? RunInline(action)
            : Dispatcher.UIThread.InvokeAsync(action).GetTask();

    private static Task RunInline(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}
