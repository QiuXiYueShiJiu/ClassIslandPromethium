// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 挪威气象研究所的 Locationforecast，免密钥。
/// </summary>
/// <remarks>
/// 数据质量很好、接口稳定，缺点是它给的是它自己那套 symbol_code，
/// 而且风是米每秒、时间是 UTC——这些单位换算都在这里一次做掉，
/// 外面拿到的永远是统一的摄氏度 / 公里每小时 / 本地时间。
/// </remarks>
public class MetNorwayProvider : IWeatherProvider
{
    private const string Endpoint = "https://api.met.no/weatherapi/locationforecast/2.0/compact";

    public WeatherProviderKind Kind => WeatherProviderKind.MetNorway;

    public string DisplayName => "MET Norway";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?lat={1:0.0000}&lon={2:0.0000}", Endpoint, query.Latitude, query.Longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);

        var timeseries = document.RootElement
            .GetProperty("properties").GetProperty("timeseries");

        var first = timeseries[0];
        var details = first.GetProperty("data").GetProperty("instant").GetProperty("details");

        var symbol = string.Empty;
        var data = first.GetProperty("data");
        if (data.TryGetProperty("next_1_hours", out var nextHour) &&
            nextHour.TryGetProperty("summary", out var summary) &&
            summary.TryGetProperty("symbol_code", out var symbolElement))
        {
            symbol = symbolElement.GetString() ?? string.Empty;
        }

        var windSpeedMs = ReadDouble(details, "wind_speed");
        var observedAt = first.TryGetProperty("time", out var timeElement) &&
                         DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture,
                             DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed.ToLocalTime()
            : DateTimeOffset.Now;

        var (todayMin, todayMax, hasRange) = ScanTodayRange(timeseries);

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = observedAt,
            RawCode = symbol,
            Condition = MapSymbol(symbol),
            Temperature = ReadDouble(details, "air_temperature"),
            // MET 不给体感温度，用气温顶上，别编一个假的出来
            FeelsLike = ReadDouble(details, "air_temperature"),
            Humidity = ReadDouble(details, "relative_humidity"),
            WindSpeed = windSpeedMs * 3.6,
            WindDirection = details.TryGetProperty("wind_from_direction", out var dir) && dir.ValueKind == JsonValueKind.Number
                ? dir.GetDouble()
                : -1d,
            Pressure = ReadDouble(details, "air_pressure_at_sea_level"),
            Precipitation = ReadDouble(first.GetProperty("data"), "next_1_hours", "details", "precipitation_amount"),
            IsDay = symbol.Contains("_night", StringComparison.OrdinalIgnoreCase) ||
                    (!symbol.Contains("_day", StringComparison.OrdinalIgnoreCase) && (observedAt.Hour is >= 6 and < 18)),
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }

    /// <summary>从时序里挑出落在当地「今天」的温度范围。</summary>
    private static (double Min, double Max, bool HasRange) ScanTodayRange(JsonElement timeseries)
    {
        var today = DateTime.Today;
        var min = double.MaxValue;
        var max = double.MinValue;
        var found = false;

        foreach (var entry in timeseries.EnumerateArray())
        {
            if (!entry.TryGetProperty("time", out var timeElement) ||
                !DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal, out var time))
            {
                continue;
            }

            if (time.ToLocalTime().Date != today)
            {
                continue;
            }

            if (!entry.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("instant", out var instant) ||
                !instant.TryGetProperty("details", out var details) ||
                !details.TryGetProperty("air_temperature", out var temperature) ||
                temperature.ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            var value = temperature.GetDouble();
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            found = true;
        }

        return found ? (min, max, true) : (0d, 0d, false);
    }

    /// <summary>把 MET 的 symbol_code 归一到通用天气现象。</summary>
    private static WeatherCondition MapSymbol(string symbol)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            return WeatherCondition.Unknown;
        }

        var s = symbol.ToLowerInvariant();
        // 顺序有讲究：先把更具体的词判掉，否则 "partlycloudy" 会被 "cloudy" 吃掉
        if (s.Contains("thunder")) return WeatherCondition.Thunderstorm;
        if (s.Contains("clearsky")) return WeatherCondition.Clear;
        if (s.Contains("fair")) return WeatherCondition.PartlyCloudy;
        if (s.Contains("partlycloudy")) return WeatherCondition.PartlyCloudy;
        if (s.Contains("fog")) return WeatherCondition.Fog;
        if (s.Contains("sleet")) return WeatherCondition.Sleet;
        if (s.Contains("snow")) return WeatherCondition.Snow;
        if (s.Contains("heavyrain")) return WeatherCondition.HeavyRain;
        if (s.Contains("drizzle")) return WeatherCondition.Drizzle;
        if (s.Contains("rain")) return WeatherCondition.Rain;
        if (s.Contains("cloudy")) return WeatherCondition.Cloudy;
        return WeatherCondition.Unknown;
    }

    private static double ReadDouble(JsonElement element, params string[] path)
    {
        foreach (var name in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out element))
            {
                return 0d;
            }
        }

        return element.ValueKind == JsonValueKind.Number ? element.GetDouble() : 0d;
    }
}
