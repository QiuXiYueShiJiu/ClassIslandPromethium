// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// Weatherbit。
/// </summary>
/// <remarks>
/// 两个要注意的点：风速是<b>米每秒</b>（这里换算成公里每小时），
/// 白天黑夜看它的 <c>pod</c> 字段（<c>d</c> 白天 / <c>n</c> 夜间）。
/// 当前天气接口不含当日温度范围，这一项留空。
/// </remarks>
public class WeatherbitProvider : KeyedWeatherProviderBase
{
    private const string Endpoint = "https://api.weatherbit.io/v2.0/current";

    public override WeatherProviderKind Kind => WeatherProviderKind.Weatherbit;

    public override string DisplayName => "Weatherbit";

    public override async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var key = RequireApiKey(query);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?lat={1:0.####}&lon={2:0.####}&key={3}&lang=zh",
            Endpoint, query.Latitude, query.Longitude, key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Weatherbit 没有返回数据，请检查密钥");
        }

        var current = data[0];
        var description = JsonRead.NestedText(current, "weather", "description");
        var pod = JsonRead.Text(current, "pod");

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = JsonRead.Nested(current, "weather", "code").ToString(CultureInfo.InvariantCulture),
            Condition = WeatherConditionText.FromAny(description),
            Temperature = JsonRead.Number(current, "temp"),
            FeelsLike = JsonRead.Number(current, "app_temp"),
            Humidity = JsonRead.Number(current, "rh"),
            // 接口给的是 m/s
            WindSpeed = JsonRead.Number(current, "wind_spd") * 3.6,
            WindDirection = JsonRead.Number(current, "wind_dir"),
            Pressure = JsonRead.Number(current, "pres"),
            Precipitation = JsonRead.Number(current, "precip"),
            IsDay = pod.Length == 0 ? GuessIsDay() : pod.Equals("d", StringComparison.OrdinalIgnoreCase),
            HasDailyRange = false
        };
    }
}
