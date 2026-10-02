// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 把报警内容套进用户写的文案模板里。
/// </summary>
/// <remarks>
/// 渲染逻辑只此一处，报警内容换什么，改这里塞进变量表的东西就行。
/// </remarks>
public static class AlertTextComposer
{
    /// <summary>天气报警可用的变量名。</summary>
    public static readonly string[] WeatherVariables =
    {
        "位置", "标题", "详情", "天气", "温度", "体感", "湿度", "风力", "风向", "最高", "最低", "数据源", "时间"
    };


    /// <summary>组装天气报警的变量表。</summary>
    public static Dictionary<string, string> BuildWeatherVariables(
        WeatherSnapshot snapshot, WeatherAlert alert, string locationName)
    {
        var culture = CultureInfo.InvariantCulture;
        return new Dictionary<string, string>
        {
            ["位置"] = locationName,
            ["标题"] = alert.Title,
            ["详情"] = alert.Detail,
            ["天气"] = WeatherText.Describe(snapshot.Condition),
            ["温度"] = snapshot.Temperature.ToString("0.#", culture) + "°C",
            ["体感"] = snapshot.FeelsLike.ToString("0.#", culture) + "°C",
            ["湿度"] = snapshot.Humidity.ToString("0.#", culture) + "%",
            ["风力"] = snapshot.WindSpeed.ToString("0.#", culture) + " km/h",
            ["风向"] = WeatherText.Direction(snapshot.WindDirection),
            ["最高"] = snapshot.HasDailyRange ? snapshot.TodayMax.ToString("0.#", culture) + "°C" : "数据源未提供",
            ["最低"] = snapshot.HasDailyRange ? snapshot.TodayMin.ToString("0.#", culture) + "°C" : "数据源未提供",
            ["数据源"] = snapshot.ProviderName,
            ["时间"] = snapshot.ObservedAt.ToString("HH:mm", culture)
        };
    }

}
