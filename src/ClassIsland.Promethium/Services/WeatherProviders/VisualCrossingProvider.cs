// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// Visual Crossing。
/// </summary>
/// <remarks>
/// 一次 timeline 请求就能拿到当前天气和当天温度范围。
/// 它不直接给白天黑夜，但给了当天的日出日落时间戳，用它算；
/// 算不出来再退回按当地时刻粗判。
/// </remarks>
public class VisualCrossingProvider : KeyedWeatherProviderBase
{
    private const string Endpoint =
        "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline";

    public override WeatherProviderKind Kind => WeatherProviderKind.VisualCrossing;

    public override string DisplayName => "Visual Crossing";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/{1:0.####},{2:0.####}/today?unitGroup=metric&include=current&contentType=json&key={3}",
            Endpoint, query.Latitude, query.Longitude, key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("currentConditions", out var current))
        {
            throw new InvalidOperationException("Visual Crossing 没有返回当前天气，请检查密钥");
        }

        var conditions = JsonRead.Text(current, "conditions");

        double todayMin = 0d, todayMax = 0d, sunrise = 0d, sunset = 0d;
        var hasRange = false;
        if (root.TryGetProperty("days", out var days) && days.GetArrayLength() > 0)
        {
            var today = days[0];
            todayMax = JsonRead.Number(today, "tempmax");
            todayMin = JsonRead.Number(today, "tempmin");
            sunrise = JsonRead.Number(today, "sunriseEpoch");
            sunset = JsonRead.Number(today, "sunsetEpoch");
            hasRange = true;
        }

        var epoch = JsonRead.Number(current, "datetimeEpoch");
        var isDay = sunrise > 0 && sunset > 0 && epoch > 0
            ? epoch >= sunrise && epoch < sunset
            : GuessIsDay();

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = conditions,
            Condition = WeatherConditionText.FromEnglish(conditions),
            Temperature = JsonRead.Number(current, "temp"),
            FeelsLike = JsonRead.Number(current, "feelslike"),
            Humidity = JsonRead.Number(current, "humidity"),
            WindSpeed = JsonRead.Number(current, "windspeed"),
            WindDirection = JsonRead.Number(current, "winddir"),
            Pressure = JsonRead.Number(current, "pressure"),
            Precipitation = JsonRead.Number(current, "precip"),
            IsDay = isDay,
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }
}
