// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>报警要往哪儿送。</summary>
public enum AlertDelivery
{
    /// <summary>不报警。</summary>
    Off = 0,

    /// <summary>只弹提醒（走 ClassIsland 的通知系统）。</summary>
    Notify = 1,

    /// <summary>只在主界面组件上实时显示。</summary>
    Inline = 2,

    /// <summary>既弹提醒，也在组件上显示。</summary>
    Both = 3
}

/// <summary>天气图标怎么画。</summary>
public enum WeatherIconMode
{
    /// <summary>用系统图标字体里的天气字形。</summary>
    SystemGlyph = 0,

    /// <summary>用 emoji，字体可指定。</summary>
    Emoji = 1,

    /// <summary>用用户自己准备的图片。</summary>
    CustomImage = 2,

    /// <summary>不画图标，只显示文字。</summary>
    TextOnly = 3
}

