// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using Avalonia.Threading;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services.EarthquakeProviders;
using ClassIsland.Promethium.Services.WeatherProviders;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 盯着关注半径内的地震，只在确实可能有震感时才出声。
/// </summary>
/// <remarks>
/// <b>这个功能能做到什么、做不到什么，必须说清楚。</b>
/// 它读的是公开地震目录，目录里的记录都是地震发生、台网定位定级之后才出现的，
/// 所以它是「事后速报」——多数情况下你感觉到晃动之后它才可能报出来。
/// 真正的震前预警要靠密集台网和专用推送通道，插件做不到。
/// <para/>
/// 也正因为如此，这里对「要不要出声」卡得很死：必须是目录里真实存在的记录，
/// 必须在关注半径内，必须达到最低震级，而且按经验规则判断可能有震感。
/// 四条缺一条都不报。宁可漏报，不报假消息。
/// </remarks>
public partial class EarthquakeMonitor : ObservableObject, IHostedService
{
    /// <summary>只保留最近这么多条去重记录，免得配置文件无限长。</summary>
    private const int MaxRememberedEvents = 300;

    private readonly EarthquakeProviderCatalog _catalog;
    private readonly PromethiumConfigStore _store;
    private readonly AlertCenter _alertCenter;

    private CancellationTokenSource? _cancellation;
    private Task? _loop;

    /// <summary>最近一次成功查到的地震条数，仅用于设置页显示状态。</summary>
    [ObservableProperty]
    private int _lastQueriedCount;

    /// <summary>最近一次抓取的错误信息。</summary>
    [ObservableProperty]
    private string _errorText = string.Empty;

    /// <summary>查到了新的、可能有震感的地震。</summary>
    public event EventHandler<EarthquakeEvent>? EarthquakeDetected;

    public EarthquakeMonitor(EarthquakeProviderCatalog catalog, PromethiumConfigStore store, AlertCenter alertCenter)
    {
        _catalog = catalog;
        _store = store;
        _alertCenter = alertCenter;
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

    /// <summary>立刻查一次。</summary>
    public Task CheckNowAsync() => CheckOnceAsync(_cancellation?.Token ?? CancellationToken.None);

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // 单轮失败不影响下一轮
            }

            var minutes = Math.Clamp(_store.Earthquake.PollIntervalMinutes, 1d, 120d);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(minutes), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CheckOnceAsync(CancellationToken token)
    {
        var config = _store.Earthquake;
        if (!config.IsEnabled)
        {
            await OnUiAsync(() =>
            {
                _alertCenter.EarthquakeAlertText = string.Empty;
                ErrorText = string.Empty;
                LastQueriedCount = 0;
            }).ConfigureAwait(false);
            return;
        }

        var provider = _catalog.Resolve(config.Source);
        var query = new EarthquakeQuery(
            _store.Weather.Latitude, _store.Weather.Longitude,
            config.RadiusKm, config.MinMagnitude, config.LookbackHours);

        IReadOnlyList<EarthquakeEvent> events;
        try
        {
            events = await provider.QueryRecentAsync(query, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await OnUiAsync(() => ErrorText = $"{provider.DisplayName} 查询失败：{ex.GetType().Name}").ConfigureAwait(false);
            return;
        }

        // 双重筛：接口那边已经按震级筛过一次，这里再确认半径和震感
        var notable = events
            .Where(e => e.DistanceKm <= config.RadiusKm)
            .Where(e => e.Magnitude >= config.MinMagnitude)
            .Where(e => e.Felt >= FeltLikelihood.Slight)
            .OrderByDescending(e => e.Time)
            .ToList();

        await OnUiAsync(() =>
        {
            ErrorText = string.Empty;
            LastQueriedCount = events.Count;
            Publish(notable);
        }).ConfigureAwait(false);
    }

    private void Publish(IReadOnlyList<EarthquakeEvent> notable)
    {
        var config = _store.Earthquake;

        // 实时显示取最新那条
        if (notable.Count == 0 || !WeatherMonitor.WantsInline(config.Delivery))
        {
            _alertCenter.EarthquakeAlertText = string.Empty;
        }
        else
        {
            var newest = notable[0];
            var variables = AlertTextComposer.BuildEarthquakeVariables(newest, _catalog.Resolve(config.Source).DisplayName);
            _alertCenter.EarthquakeAlertText = TemplateEngine.Render(config.InlineTemplate, variables);
        }

        if (!WeatherMonitor.WantsNotify(config.Delivery))
        {
            return;
        }

        foreach (var earthquake in notable)
        {
            if (string.IsNullOrEmpty(earthquake.Id) || config.NotifiedEventIds.Contains(earthquake.Id))
            {
                continue;
            }

            config.NotifiedEventIds.Add(earthquake.Id);
            EarthquakeDetected?.Invoke(this, earthquake);
        }

        // 只留最近的若干条去重记录
        if (config.NotifiedEventIds.Count > MaxRememberedEvents)
        {
            config.NotifiedEventIds.RemoveRange(0, config.NotifiedEventIds.Count - MaxRememberedEvents);
        }
    }

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
