// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Promethium.Components;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.Notifications;
using ClassIsland.Promethium.Services.WeatherProviders;
using ClassIsland.Promethium.Views.ComponentSettings;
using ClassIsland.Promethium.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Promethium;

/// <summary>
/// Pm钷 插件入口。
/// </summary>
/// <remarks>
/// 设置窗口里的入口叫「Pm优化」，各项功能作为它下面的分页挂上去。
/// </remarks>
[PluginEntrance]
public class PromethiumPlugin : PluginBase
{
    /// <summary>设置窗口里「Pm优化」这个分组的 Id。</summary>
    public const string SettingsGroupId = "qiuxiyueshijiu.promethium";

    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 全局配置存到宿主分配的本插件设置目录。
        // 先建实例、挂上自动保存，再注册进 DI，这样设置页和组件拿到的是同一个对象。
        var store = new PromethiumConfigStore(PluginConfigFolder);
        store.WatchForChanges();
        services.AddSingleton(store);

        services.AddSingleton<NominatimService>();

        // 天气数据源：三个免密钥的预设
        services.AddSingleton<OpenMeteoProvider>();
        services.AddSingleton<MetNorwayProvider>();
        services.AddSingleton<WttrInProvider>();
        services.AddSingleton(sp => new WeatherProviderCatalog(new IWeatherProvider[]
        {
            sp.GetRequiredService<OpenMeteoProvider>(),
            sp.GetRequiredService<MetNorwayProvider>(),
            sp.GetRequiredService<WttrInProvider>()
        }));


        // 报警状态的单一来源
        services.AddSingleton<AlertCenter>();

        // 监测循环：整个插件里只有它们会去拉网络数据
        services.AddSingleton<WeatherMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<WeatherMonitor>());

        // 报警走宿主自己的通知系统，用户能在 CI 的通知设置里统一管理
        services.AddNotificationProvider<WeatherAlertNotificationProvider>();

        // 设置窗口入口：Pm优化
        services.AddSettingsPageGroup(SettingsGroupId, "\uE713", "Pm优化");

        // 主界面组件「更好的天气」，第二项是它的组件设置界面
        services.AddComponent<BetterWeatherComponent, BetterWeatherComponentSettingsControl>();

        // Pm优化 下面的第一块：更好的天气
        services.AddSettingsPage<BetterWeatherSettingsPage>();
    }
}
