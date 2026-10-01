// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 按经纬度直接取天气的服务，数据来自 Open-Meteo。
/// </summary>
/// <remarks>
/// 选 Open-Meteo 的理由很实在：它直接吃经纬度、不需要注册 API key，
/// 而且预报本来就是按格点算的。宿主那套是按城市 LocationKey 查的，
/// 插件拿不到它内部的城市表，所以想要「我点的这个点」的天气只能自己查。
/// </remarks>
public class OpenMeteoService
{
    private const string Endpoint = "https://api.open-meteo.com/v1/forecast";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    /// <summary>查询指定经纬度的当前天气与今日温度范围。</summary>
    public async Task<WeatherSnapshot> QueryAsync(double latitude, double longitude, CancellationToken token = default)
    {
        var url = string.Format(
            CultureInfo.InvariantCulture,
            "{0}?latitude={1}&longitude={2}" +
            "&current=temperature_2m,relative_humidity_2m,apparent_temperature,precipitation," +
            "weather_code,pressure_msl,wind_speed_10m,wind_direction_10m,is_day" +
            "&daily=temperature_2m_max,temperature_2m_min&forecast_days=1&timezone=auto",
            Endpoint, latitude, longitude);

        using var response = await Http.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var current = root.GetProperty("current");

        var snapshot = new WeatherSnapshot
        {
            ObservedAt = DateTimeOffset.Now,
            Temperature = ReadDouble(current, "temperature_2m"),
            FeelsLike = ReadDouble(current, "apparent_temperature"),
            Humidity = ReadDouble(current, "relative_humidity_2m"),
            Precipitation = ReadDouble(current, "precipitation"),
            Pressure = ReadDouble(current, "pressure_msl"),
            WindSpeed = ReadDouble(current, "wind_speed_10m"),
            WindDirection = ReadDouble(current, "wind_direction_10m"),
            WeatherCode = (int)ReadDouble(current, "weather_code"),
            IsDay = ReadDouble(current, "is_day") > 0.5
        };

        if (root.TryGetProperty("daily", out var daily))
        {
            snapshot.TodayMax = ReadFirst(daily, "temperature_2m_max");
            snapshot.TodayMin = ReadFirst(daily, "temperature_2m_min");
        }

        return snapshot;
    }

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0d;

    private static double ReadFirst(JsonElement element, string name) =>
        element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0
            ? array[0].GetDouble()
            : 0d;
}
