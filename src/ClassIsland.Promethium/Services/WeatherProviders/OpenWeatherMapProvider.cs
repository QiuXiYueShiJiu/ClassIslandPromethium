// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// OpenWeatherMap。
/// </summary>
/// <remarks>
/// 最老牌的一家，免费额度够个人用。两个要注意的点：
/// <list type="number">
/// <item>默认单位是开尔文，必须带 <c>units=metric</c> 才是摄氏度。</item>
/// <item>风速是<b>米每秒</b>，这里换算成公里每小时，免得和别家对不上。</item>
/// </list>
/// 当前天气这个接口不返回当日温度范围（那要 One Call 接口，免费额度不含），
/// 所以这一项留空，界面会自己省掉。
/// </remarks>
public class OpenWeatherMapProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://api.openweathermap.org/data/2.5/weather";

    public override WeatherProviderKind Kind => WeatherProviderKind.OpenWeatherMap;

    public override string DisplayName => "OpenWeatherMap";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?lat={1:0.####}&lon={2:0.####}&appid={3}&units=metric&lang=zh_cn",
            Endpoint, query.Latitude, query.Longitude, key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("cod", out var cod) && cod.ValueKind == JsonValueKind.Number && cod.GetInt32() != 200)
        {
            throw new InvalidOperationException($"OpenWeatherMap 返回错误码 {cod.GetInt32()}");
        }

        var main = root.GetProperty("main");
        var weatherText = string.Empty;
        if (root.TryGetProperty("weather", out var weather) && weather.GetArrayLength() > 0)
        {
            weatherText = JsonRead.Text(weather[0], "description");
        }

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = weatherText,
            Condition = WeatherConditionText.FromAny(weatherText),
            Temperature = JsonRead.Number(main, "temp"),
            FeelsLike = JsonRead.Number(main, "feels_like"),
            Humidity = JsonRead.Number(main, "humidity"),
            // 接口给的是 m/s
            WindSpeed = JsonRead.Nested(root, "wind", "speed") * 3.6,
            WindDirection = JsonRead.Nested(root, "wind", "deg"),
            Pressure = JsonRead.Number(main, "pressure"),
            Precipitation = JsonRead.Nested(root, "rain", "1h"),
            IsDay = IsDayBySun(root),
            HasDailyRange = false
        };
    }

    /// <summary>用返回里的日出日落时间判断白天黑夜；没有就按当地时刻粗判。</summary>
    private static bool IsDayBySun(JsonElement root)
    {
        try
        {
            var now = JsonRead.Number(root, "dt");
            var sunrise = JsonRead.Nested(root, "sys", "sunrise");
            var sunset = JsonRead.Nested(root, "sys", "sunset");
            if (now > 0 && sunrise > 0 && sunset > 0)
            {
                return now >= sunrise && now < sunset;
            }
        }
        catch (Exception)
        {
            // 落回粗判
        }

        return GuessIsDay();
    }
}
