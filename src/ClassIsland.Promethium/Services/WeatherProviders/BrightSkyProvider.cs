// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 德国气象局（DWD）的观测，通过 Bright Sky 这个开放接口取得。
/// </summary>
/// <remarks>
/// <b>只覆盖德国。</b>德国以外的坐标它会明确回一句「没有匹配的数据源」，
/// 这里把那句话换成人能看懂的提示。
/// <para/>
/// 风速单位是公里每小时，气压是海平面气压，湿度字段叫 <c>relative_humidity</c>。
/// 它不给当日温度范围，也不给体感温度，这两项分别留空和退化成气温。
/// </remarks>
public class BrightSkyProvider : IWeatherProvider
{
    private const string Endpoint = "https://api.brightsky.dev/current_weather";

    public WeatherProviderKind Kind => WeatherProviderKind.BrightSky;

    public string DisplayName => "DWD 德国";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?lat={1:0.####}&lon={2:0.####}", Endpoint, query.Latitude, query.Longitude);

        string json;
        try
        {
            json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                "该坐标不在 DWD 覆盖范围内（Bright Sky 只提供德国数据），请换用别的数据源", ex);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("weather", out var weather) || weather.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("该坐标附近没有 DWD 观测站，请换用别的数据源");
        }

        var condition = JsonRead.Text(weather, "condition");
        var icon = JsonRead.Text(weather, "icon");
        var temperature = JsonRead.Number(weather, "temperature");

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.TryParse(JsonRead.Text(weather, "timestamp"),
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
                ? time.ToLocalTime()
                : DateTimeOffset.Now,
            RawCode = string.IsNullOrEmpty(icon) ? condition : $"{condition}/{icon}",
            Condition = MapCondition(condition, icon),
            Temperature = temperature,
            // DWD 当前观测不给体感温度，用气温顶上，不编一个假的出来
            FeelsLike = temperature,
            Humidity = JsonRead.Number(weather, "relative_humidity"),
            WindSpeed = JsonRead.Number(weather, "wind_speed_10"),
            WindDirection = JsonRead.Number(weather, "wind_direction_10"),
            Pressure = JsonRead.Number(weather, "pressure_msl"),
            Precipitation = JsonRead.Number(weather, "precipitation_10"),
            IsDay = icon.Contains("day", StringComparison.OrdinalIgnoreCase) ||
                    (!icon.Contains("night", StringComparison.OrdinalIgnoreCase) &&
                     DateTime.Now.Hour is >= 6 and < 18),
            HasDailyRange = false
        };
    }

    /// <summary>
    /// 按 condition 判天气现象，icon 只在 condition 认不出来时兜底。
    /// </summary>
    /// <remarks>
    /// Bright Sky 的 condition 只有 dry/fog/rain/sleet/snow/hail/thunderstorm 几种，
    /// 天空状况的细节在 icon 里（clear-day、partly-cloudy-day、cloudy……）。
    /// 所以「干」的时候要看 icon 才知道是晴还是阴。
    /// </remarks>
    private static WeatherCondition MapCondition(string condition, string icon)
    {
        var c = condition.ToLowerInvariant();
        if (c.Contains("thunder")) return WeatherCondition.Thunderstorm;
        if (c.Contains("hail")) return WeatherCondition.Hail;
        if (c.Contains("sleet")) return WeatherCondition.Sleet;
        if (c.Contains("snow")) return WeatherCondition.Snow;
        if (c.Contains("fog")) return WeatherCondition.Fog;
        if (c.Contains("rain")) return WeatherCondition.Rain;

        // 到这里说明是 dry，天空状况看 icon
        var i = icon.ToLowerInvariant();
        if (i.Contains("partly")) return WeatherCondition.PartlyCloudy;
        if (i.Contains("cloudy")) return WeatherCondition.Cloudy;
        if (i.Contains("fog")) return WeatherCondition.Fog;
        if (i.Contains("wind")) return WeatherCondition.Unknown;
        if (i.Contains("clear")) return WeatherCondition.Clear;
        return WeatherCondition.Unknown;
    }
}
