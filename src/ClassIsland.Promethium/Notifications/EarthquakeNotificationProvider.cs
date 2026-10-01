// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.IO;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Shared.Models.Notification;
using NotificationRequest = ClassIsland.Core.Models.Notification.NotificationRequest;

namespace ClassIsland.Promethium.Notifications;

/// <summary>
/// 把地震速报送进 ClassIsland 的通知系统。
/// </summary>
/// <remarks>
/// 文案里出现的每一个数字都来自地震目录原文，不做任何推算。
/// 唯一带判断成分的是「震感」，而且它在文案里被明确称为粗略判断。
/// </remarks>
[NotificationProviderInfo("6872AF1A-6E1E-4489-B0F7-7AFC66E70E2F", "Pm钷 地震速报", "\uE7BA",
    "关注范围内、按公开地震目录判定可能有震感的地震提醒。注意这是事后速报，不是震前预警。")]
public class EarthquakeNotificationProvider : NotificationProviderBase
{
    private readonly EarthquakeMonitor _monitor;
    private readonly PromethiumConfigStore _store;
    private readonly EarthquakeProviderCatalog _catalog;
    private readonly IAudioService _audioService;

    public EarthquakeNotificationProvider(
        EarthquakeMonitor monitor, PromethiumConfigStore store,
        EarthquakeProviderCatalog catalog, IAudioService audioService)
    {
        _monitor = monitor;
        _store = store;
        _catalog = catalog;
        _audioService = audioService;
        _monitor.EarthquakeDetected += OnEarthquakeDetected;
    }

    private void OnEarthquakeDetected(object? sender, EarthquakeEvent earthquake)
    {
        var config = _store.Earthquake;
        var variables = AlertTextComposer.BuildEarthquakeVariables(
            earthquake, _catalog.Resolve(config.Source).DisplayName);
        var text = TemplateEngine.Render(config.NotifyTemplate, variables);

        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateTwoIconsMask("地震速报", rightIcon: "\uE7BA"),
            OverlayContent = NotificationContent.CreateSimpleTextContent(text, content =>
            {
                content.Duration = TimeSpan.FromSeconds(60);
                content.IsSpeechEnabled = true;
                content.SpeechContent = text;
            })
        };

        if (!string.IsNullOrWhiteSpace(config.SoundPath) && File.Exists(config.SoundPath))
        {
            request.RequestNotificationSettings = new NotificationSettings
            {
                IsNotificationSoundEnabled = false
            };
            _ = _audioService.PlayAudioAsync(config.SoundPath, (float)Math.Clamp(config.SoundVolume, 0d, 1d), null);
        }

        ShowNotification(request);
    }
}
