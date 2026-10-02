// Pm钷 —— ClassIsland 综合增强插件
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 当前报警状态的唯一来源。
/// </summary>
/// <remarks>
/// 检测逻辑只往这里写，主界面组件只从这里读，两边不直接打交道。
/// 这样「实时显示」和「弹提醒」看的一定是同一份判断结果，不会出现
/// 组件上还挂着高温提示、通知却已经过期这种事。
/// </remarks>
public partial class AlertCenter : ObservableObject
{
    /// <summary>当前天气报警的显示文字，空串表示没有。</summary>
    [ObservableProperty]
    private string _weatherAlertText = string.Empty;

    /// <summary>当前有没有天气报警。</summary>
    public bool HasWeatherAlert => !string.IsNullOrEmpty(WeatherAlertText);


    partial void OnWeatherAlertTextChanged(string value) => OnPropertyChanged(nameof(HasWeatherAlert));
}
