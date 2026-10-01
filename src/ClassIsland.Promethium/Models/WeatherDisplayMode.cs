// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 组件上显示多少东西。
/// </summary>
/// <remarks>
/// 挑预设是为了省事；真要一项一项抠，选「自定义」再用下面那些开关。
/// 枚举顺序和设置页下拉框的顺序一致，靠索引对齐。
/// </remarks>
public enum WeatherDisplayMode
{
    /// <summary>标准：天气、气温、今日温差、湿度、位置名。</summary>
    Standard = 0,

    /// <summary>紧凑：只留图标、气温、天气。</summary>
    Compact = 1,

    /// <summary>详细：能显示的全都显示。</summary>
    Detailed = 2,

    /// <summary>自定义：完全听逐项开关的。</summary>
    Custom = 3
}
