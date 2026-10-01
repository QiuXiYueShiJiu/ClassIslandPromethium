// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Promethium.Components;
using ClassIsland.Promethium.Services;
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
        // 全局配置存到宿主分配的本插件设置目录
        services.AddSingleton(new PromethiumConfigStore(PluginConfigFolder));

        services.AddSingleton<OpenMeteoService>();
        services.AddSingleton<NominatimService>();

        // 设置窗口入口：Pm优化
        services.AddSettingsPageGroup(SettingsGroupId, "\uE713", "Pm优化");

        // 主界面组件「更好的天气」，第二项是它的组件设置界面
        services.AddComponent<BetterWeatherComponent, BetterWeatherComponentSettingsControl>();

        // Pm优化 下面的第一块：更好的天气
        services.AddSettingsPage<BetterWeatherSettingsPage>();
    }
}
