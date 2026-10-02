// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 7Timer，免密钥。
/// </summary>
/// <remarks>
/// 用它可以，但必须知道它的三个短板，所以界面上要如实标出来：
/// <list type="number">
/// <item>它是<b>预报</b>不是观测，而且从 init 时刻起按 3 小时一档给，
///       所谓「当前」其实是最接近的一档，最多能差 3 小时。</item>
/// <item>风速只给<b>蒲福风级</b>（0~12 整数），换算成风速只能是近似值。</item>
/// <item>降水量给的是 <b>0~9 的等级码而不是毫米</b>，所以本插件不把它当作降水量用，
///       一律记 0——宁可缺这一项，也不能拿等级码当毫米去比报警阈值。</item>
/// </list>
/// </remarks>
public class SevenTimerProvider : IWeatherProvider
{
    private const string Endpoint = "https://www.7timer.info/bin/api.pl";

    public WeatherProviderKind Kind => WeatherProviderKind.SevenTimer;

    public string DisplayName => "7Timer";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?lon={1:0.0000}&lat={2:0.0000}&product=civil&output=json",
            Endpoint, query.Longitude, query.Latitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("dataseries", out var series) || series.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("7Timer 返回里没有预报序列");
        }

        // init 形如 2026100118，是 UTC 的 yyyyMMddHH
        var init = ParseInit(JsonRead.Text(root, "init"));
        var first = series[0];

        var weatherToken = JsonRead.Text(first, "weather");
        var isDay = !weatherToken.EndsWith("night", StringComparison.OrdinalIgnoreCase);

        var (todayMin, todayMax, hasRange) = ScanTodayRange(series, init);

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = init.AddHours(JsonRead.Number(first, "timepoint")).ToLocalTime(),
            RawCode = weatherToken,
            Condition = MapToken(weatherToken),
            Temperature = JsonRead.Number(first, "temp2m"),
            // 7Timer 不给体感温度，用气温顶上，不编一个假的出来
            FeelsLike = JsonRead.Number(first, "temp2m"),
            Humidity = JsonRead.Number(first, "rh2m"),
            WindSpeed = WeatherText.BeaufortToKmh((int)JsonRead.Nested(first, "wind10m", "speed")),
            WindDirection = WeatherText.DirectionFromCompass(JsonRead.NestedText(first, "wind10m", "direction")),
            // 7Timer 没有气压，也没有可用的毫米降水量
            Pressure = 0d,
            Precipitation = 0d,
            IsDay = isDay,
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }

    private static DateTimeOffset ParseInit(string init)
    {
        if (init.Length >= 10 &&
            DateTimeOffset.TryParseExact(init.Substring(0, 10), "yyyyMMddHH",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    /// <summary>挑出落在当地「今天」的那些档，取温度范围。</summary>
    private static (double Min, double Max, bool HasRange) ScanTodayRange(JsonElement series, DateTimeOffset init)
    {
        var today = DateTime.Today;
        var min = double.MaxValue;
        var max = double.MinValue;
        var found = false;

        foreach (var entry in series.EnumerateArray())
        {
            var time = init.AddHours(JsonRead.Number(entry, "timepoint")).ToLocalTime();
            if (time.Date != today)
            {
                continue;
            }

            var temperature = JsonRead.Number(entry, "temp2m");
            min = Math.Min(min, temperature);
            max = Math.Max(max, temperature);
            found = true;
        }

        return found ? (min, max, true) : (0d, 0d, false);
    }

    /// <summary>7Timer 的天气词映射。</summary>
    private static WeatherCondition MapToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return WeatherCondition.Unknown;
        }

        var t = token.ToLowerInvariant();
        // 顺序关键：mcloudy / pcloudy 都含 cloudy，先判它们再判 cloudy
        if (t.StartsWith("ts", StringComparison.Ordinal)) return WeatherCondition.Thunderstorm;
        if (t.Contains("rainsnow")) return WeatherCondition.Sleet;
        if (t.Contains("snow")) return WeatherCondition.Snow;
        if (t.Contains("fog")) return WeatherCondition.Fog;
        if (t.Contains("humid")) return WeatherCondition.Haze;
        if (t.Contains("lightrain")) return WeatherCondition.Rain;
        if (t.Contains("shower")) return WeatherCondition.Rain;
        if (t.Contains("mcloudy")) return WeatherCondition.Cloudy;
        if (t.Contains("pcloudy")) return WeatherCondition.PartlyCloudy;
        if (t.Contains("rain")) return WeatherCondition.HeavyRain;
        if (t.Contains("cloudy")) return WeatherCondition.Overcast;
        if (t.Contains("clear")) return WeatherCondition.Clear;
        // windy 只说明风大，说明不了天空状况，不猜
        return WeatherCondition.Unknown;
    }
}
