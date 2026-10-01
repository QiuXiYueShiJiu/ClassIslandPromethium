// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// Open-Meteo：直接按经纬度查，免密钥。
/// </summary>
/// <remarks>
/// 默认数据源。选它的主要理由是接口原生吃经纬度，和本插件「地图上点哪儿看哪儿」
/// 的做法对得上；而且返回的是 WMO 标准码，映射关系清楚可查。
/// </remarks>
public class OpenMeteoProvider : IWeatherProvider
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";

    public WeatherProviderKind Kind => WeatherProviderKind.OpenMeteo;

    public string DisplayName => "Open-Meteo";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?latitude={1}&longitude={2}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation," +
            "weather_code,pressure_msl,wind_speed_10m,wind_direction_10m,is_day" +
            "&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto",
            Endpoint, query.Latitude, query.Longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var current = root.GetProperty("current");
        var code = (int)ReadDouble(current, "weather_code");

        var snapshot = new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = code.ToString(CultureInfo.InvariantCulture),
            Condition = MapCode(code),
            Temperature = ReadDouble(current, "temperature_2m"),
            FeelsLike = ReadDouble(current, "apparent_temperature"),
            Humidity = ReadDouble(current, "relative_humidity_2m"),
            Precipitation = ReadDouble(current, "precipitation"),
            Pressure = ReadDouble(current, "pressure_msl"),
            WindSpeed = ReadDouble(current, "wind_speed_10m"),
            WindDirection = ReadDouble(current, "wind_direction_10m"),
            IsDay = ReadDouble(current, "is_day") > 0.5
        };

        if (root.TryGetProperty("daily", out var daily))
        {
            snapshot.TodayMax = ReadFirst(daily, "temperature_2m_max");
            snapshot.TodayMin = ReadFirst(daily, "temperature_2m_min");
            snapshot.HasDailyRange = true;
        }

        return snapshot;
    }

    /// <summary>WMO 气象码映射。</summary>
    private static WeatherCondition MapCode(int code) => code switch
    {
        0 => WeatherCondition.Clear,
        1 => WeatherCondition.PartlyCloudy,
        2 => WeatherCondition.Cloudy,
        3 => WeatherCondition.Overcast,
        45 or 48 => WeatherCondition.Fog,
        51 or 53 or 55 or 56 or 57 => WeatherCondition.Drizzle,
        61 or 63 or 66 or 67 => WeatherCondition.Rain,
        65 => WeatherCondition.HeavyRain,
        71 or 73 or 75 or 77 => WeatherCondition.Snow,
        80 or 81 => WeatherCondition.Rain,
        82 => WeatherCondition.HeavyRain,
        85 or 86 => WeatherCondition.Snow,
        95 => WeatherCondition.Thunderstorm,
        96 or 99 => WeatherCondition.Hail,
        _ => WeatherCondition.Unknown
    };

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0d;

    private static double ReadFirst(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0
            ? array[0].GetDouble()
            : 0d;
}
