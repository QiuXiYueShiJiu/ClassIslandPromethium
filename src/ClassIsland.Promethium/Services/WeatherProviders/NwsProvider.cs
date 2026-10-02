// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 美国国家气象局（NWS / weather.gov），免密钥。
/// </summary>
/// <remarks>
/// <b>只覆盖美国。</b>美国以外的坐标会直接查不到网格点，这是服务本身的限制，不是插件的问题。
/// <para/>
/// 另外它的返回有两个坑，都在这里处理掉了：
/// <list type="number">
/// <item>温度默认是<b>华氏度</b>，要按 <c>temperatureUnit</c> 判断后再换算。</item>
/// <item>风速是 <c>"10 to 15 mph"</c> 这样的<b>字符串</b>，风向是 <c>WNW</c> 这样的字母。</item>
/// </list>
/// 它不提供气压，<c>probabilityOfPrecipitation</c> 给的是<b>降水概率百分比</b>而不是毫米，
/// 所以这两项分别记 0，不拿去充当别的含义。
/// </remarks>
public class NwsProvider : IWeatherProvider
{
    private const string Endpoint = "https://api.weather.gov";
    private const double MilesPerHourToKmh = 1.609344;

    public WeatherProviderKind Kind => WeatherProviderKind.Nws;

    public string DisplayName => "NWS（仅美国）";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        // 第一跳：坐标 -> 网格点，拿到逐小时预报的地址
        var pointUrl = string.Format(CultureInfo.InvariantCulture,
            "{0}/points/{1:0.0000},{2:0.0000}", Endpoint, query.Latitude, query.Longitude);
        string pointJson;
        try
        {
            pointJson = await HttpClients.Shared.GetStringAsync(pointUrl, token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // 美国以外的坐标 NWS 直接返回 404。对用户说「404」等于没说，
            // 换成一句能看懂的话。
            throw new InvalidOperationException(
                "该坐标不在 NWS 覆盖范围内（NWS 只提供美国数据），请换用别的数据源", ex);
        }

        string? hourlyUrl;
        using (var pointDocument = JsonDocument.Parse(pointJson))
        {
            hourlyUrl = JsonRead.NestedText(pointDocument.RootElement, "properties", "forecastHourly");
        }

        if (string.IsNullOrEmpty(hourlyUrl))
        {
            throw new InvalidOperationException("该坐标不在 NWS 覆盖范围内（NWS 只提供美国数据）");
        }

        // 第二跳：逐小时预报
        var hourlyJson = await HttpClients.Shared.GetStringAsync(hourlyUrl, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(hourlyJson);

        if (!document.RootElement.TryGetProperty("properties", out var properties) ||
            !properties.TryGetProperty("periods", out var periods) || periods.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("NWS 返回里没有预报时段");
        }

        var first = periods[0];
        var temperature = ToCelsius(JsonRead.Number(first, "temperature"), JsonRead.Text(first, "temperatureUnit"));
        var shortForecast = JsonRead.Text(first, "shortForecast");

        var (todayMin, todayMax, hasRange) = ScanTodayRange(periods);

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = ParseTime(JsonRead.Text(first, "startTime")),
            RawCode = shortForecast,
            Condition = WeatherConditionText.FromEnglish(shortForecast),
            Temperature = temperature,
            // NWS 逐小时预报不给体感温度，用气温顶上，不编一个假的出来
            FeelsLike = temperature,
            Humidity = JsonRead.Nested(first, "relativeHumidity", "value"),
            WindSpeed = JsonRead.MaxNumberInText(JsonRead.Text(first, "windSpeed")) * MilesPerHourToKmh,
            WindDirection = WeatherText.DirectionFromCompass(JsonRead.Text(first, "windDirection")),
            // 气压没有；降水概率是百分比不是毫米，都不能往这两个字段里塞
            Pressure = 0d,
            Precipitation = 0d,
            IsDay = JsonRead.Flag(first, "isDaytime"),
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }

    private static double ToCelsius(double value, string unit) =>
        unit.Equals("F", StringComparison.OrdinalIgnoreCase) ? (value - 32d) * 5d / 9d : value;

    private static DateTimeOffset ParseTime(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToLocalTime()
            : DateTimeOffset.Now;

    /// <summary>扫当地的今天那一批逐小时预报，取温度范围。</summary>
    private static (double Min, double Max, bool HasRange) ScanTodayRange(JsonElement periods)
    {
        var today = DateTime.Today;
        var min = double.MaxValue;
        var max = double.MinValue;
        var found = false;

        foreach (var period in periods.EnumerateArray())
        {
            var time = ParseTime(JsonRead.Text(period, "startTime"));
            if (time.Date != today)
            {
                continue;
            }

            var temperature = ToCelsius(JsonRead.Number(period, "temperature"), JsonRead.Text(period, "temperatureUnit"));
            min = Math.Min(min, temperature);
            max = Math.Max(max, temperature);
            found = true;
        }

        return found ? (min, max, true) : (0d, 0d, false);
    }
}
