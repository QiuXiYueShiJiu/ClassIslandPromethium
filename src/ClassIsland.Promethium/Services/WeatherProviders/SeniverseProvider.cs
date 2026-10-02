// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 心知天气（Seniverse）。
/// </summary>
/// <remarks>
/// 国内另一家常用服务，同样需要自己申请密钥。
/// 它的 <c>wind_direction</c> 给的是「东南」这样的中文方位而不是角度，
/// 这里通过罗盘表反查角度；查不到就记 -1，界面会显示成「风向未知」而不是编一个角度。
/// </remarks>
public class SeniverseProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://api.seniverse.com/v3/weather";

    public override WeatherProviderKind Kind => WeatherProviderKind.Seniverse;

    public override string DisplayName => "心知天气";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        // 心知是「纬度:经度」，中间冒号
        var location = string.Format(CultureInfo.InvariantCulture, "{0:0.####}:{1:0.####}",
            query.Latitude, query.Longitude);

        var nowJson = await HttpClients.Shared
            .GetStringAsync($"{Endpoint}/now.json?key={key}&location={location}&language=zh-Hans&unit=c", token)
            .ConfigureAwait(false);

        using var document = JsonDocument.Parse(nowJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("心知天气没有返回结果，请检查密钥与坐标");
        }

        var now = results[0].GetProperty("now");

        double todayMin = 0d, todayMax = 0d;
        var hasRange = false;
        try
        {
            var dailyJson = await HttpClients.Shared
                .GetStringAsync($"{Endpoint}/daily.json?key={key}&location={location}&language=zh-Hans&unit=c&start=0&days=1", token)
                .ConfigureAwait(false);
            using var dailyDocument = JsonDocument.Parse(dailyJson);
            if (dailyDocument.RootElement.TryGetProperty("results", out var dailyResults) &&
                dailyResults.GetArrayLength() > 0 &&
                dailyResults[0].TryGetProperty("daily", out var daily) && daily.GetArrayLength() > 0)
            {
                todayMax = JsonRead.Number(daily[0], "high");
                todayMin = JsonRead.Number(daily[0], "low");
                hasRange = true;
            }
        }
        catch (Exception)
        {
            // 拿不到预报不影响当前天气
        }

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = JsonRead.Text(now, "code"),
            Condition = WeatherConditionText.FromChinese(JsonRead.Text(now, "text")),
            Temperature = JsonRead.Number(now, "temperature"),
            FeelsLike = JsonRead.Number(now, "feels_like"),
            Humidity = JsonRead.Number(now, "humidity"),
            WindSpeed = JsonRead.Number(now, "wind_speed"),
            WindDirection = WeatherText.DirectionFromChineseCompass(JsonRead.Text(now, "wind_direction")),
            Pressure = JsonRead.Number(now, "pressure"),
            Precipitation = JsonRead.Number(now, "precipitation"),
            IsDay = GuessIsDay(),
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }
}
