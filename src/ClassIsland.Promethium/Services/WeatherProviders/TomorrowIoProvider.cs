// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// Tomorrow.io。
/// </summary>
/// <remarks>
/// 它给的是数值 <c>weatherCode</c> 而不是文字描述，所以这里按官方码表映射。
/// 免费额度调用次数偏紧，适合当备选。
/// </remarks>
public class TomorrowIoProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://api.tomorrow.io/v4/weather/realtime";

    public override WeatherProviderKind Kind => WeatherProviderKind.TomorrowIo;

    public override string DisplayName => "Tomorrow.io";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?location={1:0.####},{2:0.####}&units=metric&apikey={3}",
            Endpoint, query.Latitude, query.Longitude, key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("values", out var values))
        {
            throw new InvalidOperationException("Tomorrow.io 没有返回数据，请检查密钥");
        }

        var code = (int)JsonRead.Number(values, "weatherCode");

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = code.ToString(CultureInfo.InvariantCulture),
            Condition = MapCode(code),
            Temperature = JsonRead.Number(values, "temperature"),
            FeelsLike = JsonRead.Number(values, "temperatureApparent"),
            Humidity = JsonRead.Number(values, "humidity"),
            WindSpeed = JsonRead.Number(values, "windSpeed"),
            WindDirection = JsonRead.Number(values, "windDirection"),
            Pressure = JsonRead.Number(values, "pressureSeaLevel"),
            Precipitation = JsonRead.Number(values, "precipitationIntensity"),
            IsDay = GuessIsDay(),
            HasDailyRange = false
        };
    }

    /// <summary>Tomorrow.io 官方天气码映射。</summary>
    private static WeatherCondition MapCode(int code) => code switch
    {
        1000 => WeatherCondition.Clear,            // Clear
        1100 => WeatherCondition.Clear,            // Mostly Clear
        1101 => WeatherCondition.PartlyCloudy,     // Partly Cloudy
        1102 => WeatherCondition.Cloudy,           // Mostly Cloudy
        1001 => WeatherCondition.Overcast,         // Cloudy
        2000 or 2100 => WeatherCondition.Fog,      // Fog / Light Fog
        4000 => WeatherCondition.Drizzle,          // Drizzle
        4001 or 4200 => WeatherCondition.Rain,     // Rain / Light Rain
        4201 => WeatherCondition.HeavyRain,        // Heavy Rain
        5000 or 5001 or 5100 => WeatherCondition.Snow,
        5101 => WeatherCondition.Snow,             // Heavy Snow
        6000 or 6001 or 6200 or 6201 => WeatherCondition.Sleet,
        7000 or 7101 or 7102 => WeatherCondition.Hail,
        8000 => WeatherCondition.Thunderstorm,
        _ => WeatherCondition.Unknown
    };
}
