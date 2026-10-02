// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 和风天气（QWeather）。
/// </summary>
/// <remarks>
/// 国内可用性最好的一个，代价是要自己去官网申请一个免费密钥。
/// <para/>
/// 注意：和风现在会给每个账号分配一个专属 API Host（形如 <c>xxxx.re.qweatherapi.com</c>），
/// 这里用的是公共的开发域名 <c>devapi.qweather.com</c>。如果你的密钥绑定了专属 Host，
/// 这个预设可能连不上，得改用支持自定义 Host 的方式。
/// </remarks>
public class QWeatherProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://devapi.qweather.com/v7/weather";

    public override WeatherProviderKind Kind => WeatherProviderKind.QWeather;

    public override string DisplayName => "和风天气";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        // 和风的 location 是「经度,纬度」，顺序和别家相反，容易写错
        var location = string.Format(CultureInfo.InvariantCulture, "{0:0.####},{1:0.####}",
            query.Longitude, query.Latitude);

        var nowJson = await HttpClients.Shared
            .GetStringAsync($"{Endpoint}/now?location={location}&key={key}&lang=zh", token).ConfigureAwait(false);

        using var document = JsonDocument.Parse(nowJson);
        var root = document.RootElement;
        if (JsonRead.Text(root, "code") != "200")
        {
            throw new InvalidOperationException($"和风天气返回错误码 {JsonRead.Text(root, "code")}");
        }

        var now = root.GetProperty("now");

        // 再要一次 3 天预报，只为拿今天的最低/最高温
        double todayMin = 0d, todayMax = 0d;
        var hasRange = false;
        try
        {
            var dailyJson = await HttpClients.Shared
                .GetStringAsync($"{Endpoint}/3d?location={location}&key={key}&lang=zh", token).ConfigureAwait(false);
            using var dailyDocument = JsonDocument.Parse(dailyJson);
            if (dailyDocument.RootElement.TryGetProperty("daily", out var daily) && daily.GetArrayLength() > 0)
            {
                todayMax = JsonRead.Number(daily[0], "tempMax");
                todayMin = JsonRead.Number(daily[0], "tempMin");
                hasRange = true;
            }
        }
        catch (Exception)
        {
            // 拿不到预报不影响当前天气，只是少一项
        }

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = JsonRead.Text(now, "icon"),
            Condition = WeatherConditionText.FromChinese(JsonRead.Text(now, "text")),
            Temperature = JsonRead.Number(now, "temp"),
            FeelsLike = JsonRead.Number(now, "feelsLike"),
            Humidity = JsonRead.Number(now, "humidity"),
            WindSpeed = JsonRead.Number(now, "windSpeed"),
            WindDirection = JsonRead.Number(now, "wind360"),
            Pressure = JsonRead.Number(now, "pressure"),
            Precipitation = JsonRead.Number(now, "precip"),
            IsDay = GuessIsDay(),
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }
}
