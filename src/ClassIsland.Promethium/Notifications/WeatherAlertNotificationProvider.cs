// Pm钷 —— ClassIsland 综合增强插件
using System.IO;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Shared.Models.Notification;
using Microsoft.Extensions.Hosting;
using NotificationRequest = ClassIsland.Core.Models.Notification.NotificationRequest;

namespace ClassIsland.Promethium.Notifications;

/// <summary>
/// 把天气报警送进 ClassIsland 自己的通知系统。
/// </summary>
/// <remarks>
/// 走宿主的通知系统而不是自己弹窗，好处是遮罩/悬浮、朗读、免打扰这些
/// 用户已经在用的设置全都自动生效，用户还能在 CI 的通知设置里单独关掉它。
/// </remarks>
[NotificationProviderInfo("D5FE227F-4B9D-4157-B714-BE9B5285E62A", "Pm钷 天气报警", "\uE7BA",
    "根据真实预报数据判断的高温、低温、大风、强降水、雷暴、大雾提醒。")]
public class WeatherAlertNotificationProvider : NotificationProviderBase
{
    private readonly WeatherMonitor _monitor;
    private readonly PromethiumConfigStore _store;
    private readonly IAudioService _audioService;

    public WeatherAlertNotificationProvider(WeatherMonitor monitor, PromethiumConfigStore store, IAudioService audioService)
    {
        _monitor = monitor;
        _store = store;
        _audioService = audioService;
        _monitor.AlertRaised += OnAlertRaised;
    }

    private void OnAlertRaised(object? sender, WeatherAlert alert)
    {
        var config = _store.Weather;
        var snapshot = _monitor.Snapshot;
        if (snapshot == null)
        {
            return;
        }

        var variables = AlertTextComposer.BuildWeatherVariables(snapshot, alert, config.LocationName);
        var text = TemplateEngine.Render(config.NotifyTemplate, variables);
        Publish(alert.Title + "报警", text, config.AlertGlyph, config.SoundPath, config.SoundVolume);
    }

    private void Publish(string title, string message, string glyph, string soundPath, double volume)
    {
        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask(title, rightIcon: glyph),
            OverlayContent = NotificationContent.CreateSimpleTextContent(message, content =>
            {
                content.Duration = TimeSpan.FromSeconds(30);
                content.IsSpeechEnabled = true;
                content.SpeechContent = message;
            })
        };

        ApplySound(request, soundPath, volume);
        ShowNotification(request);
    }

    /// <summary>
    /// 处理自定义提示音。
    /// </summary>
    /// <remarks>
    /// 用户指定了自己的音频就自己放，同时把宿主的提示音关掉——
    /// 不然两个声音会叠在一起响。
    /// </remarks>
    private void ApplySound(NotificationRequest request, string soundPath, double volume)
    {
        if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
        {
            return;
        }

        request.RequestNotificationSettings = new NotificationSettings
        {
            IsNotificationSoundEnabled = false
        };

        _ = _audioService.PlayAudioAsync(soundPath, (float)Math.Clamp(volume, 0d, 1d), null);
    }
}
