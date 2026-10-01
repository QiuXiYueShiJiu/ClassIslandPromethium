// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.Promethium;

/// <summary>
/// Pm钷 插件入口。
/// </summary>
/// <remarks>
/// 骨架阶段：只把插件挂起来，确认能被宿主加载。
/// 具体增强项按清单逐个加，每加一项都要保持能编译、能加载。
/// </remarks>
public class PromethiumPlugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 功能按扩展点分块注册，先留空。
        // 主界面组件  -> services.AddComponent<...>()
        // 自动化动作  -> services.AddAction<...>()
        // 提醒提供方  -> services.AddNotificationProvider<...>()
        // 设置页      -> services.AddSettingsPage<...>()
    }
}
