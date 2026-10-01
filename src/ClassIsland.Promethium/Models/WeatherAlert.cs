// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
namespace ClassIsland.Promethium.Models;

/// <summary>
/// 一条由真实预报数据判定出来的天气报警。
/// </summary>
/// <remarks>
/// <b>这不是气象台发布的预警信号。</b>气象台发预警要有资质、要人工会商，
/// 一个插件只能根据拿到的观测/预报数值去比阈值。所以这里用的字眼是
/// 「报警」而不是「预警」，文案里也不允许出现「xx预警信号」这种说法。
/// </remarks>
/// <param name="Key">报警类型标识，用于去重。</param>
/// <param name="Title">短标题，例如「高温」。</param>
/// <param name="Detail">具体数值说明。</param>
public record WeatherAlert(string Key, string Title, string Detail);
