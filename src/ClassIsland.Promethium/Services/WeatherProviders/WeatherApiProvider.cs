// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// WeatherAPI.com。
/// </summary>
/// <remarks>
/// 用它的 <c>forecast.json</c> 而不是 <c>current.json</c>：一次请求就能同时拿到
/// 当前天气和当天温度范围，省一次往返。免费额度对个人足够。
/// 它的 <c>is_day</c> 字段直接给白天黑夜，不用自己算。
/// </remarks>
public class WeatherApiProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://api.weatherapi.com/v1/forecast.json";

    public override WeatherProviderKind Kind => WeatherProviderKind.WeatherApi;

    public override string DisplayName => "WeatherAPI";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?key={1}&q={2:0.####},{3:0.####}&days=1&lang=zh&aqi=no&alerts=no",
            Endpoint, key, query.Latitude, query.Longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("current", out var current))
        {
            throw new InvalidOperationException("WeatherAPI 没有返回当前天气，请检查密钥");
        }

        var conditionText = JsonRead.NestedText(current, "condition", "text");

        double todayMin = 0d, todayMax = 0d;
        var hasRange = false;
        if (root.TryGetProperty("forecast", out var forecast) &&
            forecast.TryGetProperty("forecastday", out var days) && days.GetArrayLength() > 0 &&
            days[0].TryGetProperty("day", out var day))
        {
            todayMax = JsonRead.Number(day, "maxtemp_c");
            todayMin = JsonRead.Number(day, "mintemp_c");
            hasRange = true;
        }

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = JsonRead.NestedText(current, "condition", "code"),
            Condition = WeatherConditionText.FromAny(conditionText),
            Temperature = JsonRead.Number(current, "temp_c"),
            FeelsLike = JsonRead.Number(current, "feelslike_c"),
            Humidity = JsonRead.Number(current, "humidity"),
            WindSpeed = JsonRead.Number(current, "wind_kph"),
            WindDirection = JsonRead.Number(current, "wind_degree"),
            Pressure = JsonRead.Number(current, "pressure_mb"),
            Precipitation = JsonRead.Number(current, "precip_mm"),
            IsDay = JsonRead.Flag(current, "is_day"),
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }
}
