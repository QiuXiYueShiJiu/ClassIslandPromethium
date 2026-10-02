// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 拿预报数值去比用户设的阈值，判断要不要报警。
/// </summary>
/// <remarks>
/// 这里只做一件事：比较。所有输入都来自数据源的真实返回，
/// 判断规则也是明摆着的阈值，没有任何推测成分——不会出现「可能要有暴雨了」这种话。
/// </remarks>
public static class WeatherAlertEvaluator
{
    /// <summary>
    /// 评估一份天气快照，返回所有命中的报警。
    /// </summary>
    public static IReadOnlyList<WeatherAlert> Evaluate(WeatherSnapshot snapshot, WeatherConfig config)
    {
        var alerts = new List<WeatherAlert>();
        var culture = CultureInfo.InvariantCulture;

        // 高温取「今日最高」和当前气温里更大的那个：预报说今天要冲到 38 度，
        // 就不该等到现在真的 38 度才提醒。
        var hottest = Math.Max(snapshot.Temperature,
            snapshot.HasDailyRange ? snapshot.TodayMax : snapshot.Temperature);
        if (hottest >= config.HighTemperatureThreshold)
        {
            alerts.Add(new WeatherAlert("high_temperature", "高温",
                $"最高 {hottest.ToString("0.#", culture)}°C，已达到你设的 {config.HighTemperatureThreshold.ToString("0.#", culture)}°C"));
        }

        var coldest = Math.Min(snapshot.Temperature,
            snapshot.HasDailyRange ? snapshot.TodayMin : snapshot.Temperature);
        if (coldest <= config.LowTemperatureThreshold)
        {
            alerts.Add(new WeatherAlert("low_temperature", "低温",
                $"最低 {coldest.ToString("0.#", culture)}°C，已达到你设的 {config.LowTemperatureThreshold.ToString("0.#", culture)}°C"));
        }

        if (snapshot.WindSpeed >= config.WindSpeedThreshold)
        {
            alerts.Add(new WeatherAlert("wind", "大风",
                $"当前风速 {snapshot.WindSpeed.ToString("0.#", culture)} km/h，已达到你设的 {config.WindSpeedThreshold.ToString("0.#", culture)} km/h"));
        }

        // 注意：这里比的是数据源给的「当前时段降水量」，单位毫米，
        // 不是全天累计，所以设置页里写的是「每小时降水量阈值」。
        if (snapshot.Precipitation >= config.PrecipitationThreshold)
        {
            alerts.Add(new WeatherAlert("precipitation", "强降水",
                $"降水量 {snapshot.Precipitation.ToString("0.#", culture)} mm，已达到你设的 {config.PrecipitationThreshold.ToString("0.#", culture)} mm"));
        }

        if (config.AlertThunderstorm && snapshot.Condition == WeatherCondition.Thunderstorm)
        {
            alerts.Add(new WeatherAlert("thunderstorm", "雷暴", "当前天气为雷阵雨"));
        }

        if (config.AlertFog && snapshot.Condition is WeatherCondition.Fog or WeatherCondition.Haze)
        {
            alerts.Add(new WeatherAlert("fog", "大雾或霾",
                $"当前{WeatherText.Describe(snapshot.Condition)}"));
        }

        return alerts;
    }
}
