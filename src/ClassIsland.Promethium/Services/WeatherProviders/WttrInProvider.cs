// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// wttr.in，免密钥。
/// </summary>
/// <remarks>
/// 用起来最省事的一个：地址里直接写经纬度就行。
/// 但它自己是个聚合服务，底下换了什么数据源不会告诉你，
/// 所以拿到的数值偶尔会和别家对不上，心里要有数。
/// </remarks>
public class WttrInProvider : IWeatherProvider
{
    public WeatherProviderKind Kind => WeatherProviderKind.WttrIn;

    public string DisplayName => "wttr.in";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "https://wttr.in/{0:0.0000},{1:0.0000}?format=j1", query.Latitude, query.Longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var current = root.GetProperty("current_condition")[0];
        var code = ParseInt(current, "weatherCode");

        // wttr.in 会冒出没在公开码表里的编号（实测 149 = Smoky haze）。
        // 所以编号认不出来时，退回去认它的文字描述，别让天气显示成「未知」。
        var condition = MapCode(code);
        var description = ReadDescription(current);
        if (condition == WeatherCondition.Unknown)
        {
            condition = MapDescription(description);
        }

        double todayMin = 0d, todayMax = 0d;
        var hasRange = false;
        if (root.TryGetProperty("weather", out var weather) && weather.GetArrayLength() > 0)
        {
            var day = weather[0];
            todayMin = ReadDouble(day, "mintempC");
            todayMax = ReadDouble(day, "maxtempC");
            hasRange = true;
        }

        var hour = DateTime.Now.Hour;

        return new WeatherSnapshot
        {
            ProviderName = DisplayName,
            ObservedAt = DateTimeOffset.Now,
            RawCode = string.IsNullOrEmpty(description)
                ? code.ToString(CultureInfo.InvariantCulture)
                : $"{code} ({description})",
            Condition = condition,
            Temperature = ReadDouble(current, "temp_C"),
            FeelsLike = ReadDouble(current, "FeelsLikeC"),
            Humidity = ReadDouble(current, "humidity"),
            WindSpeed = ReadDouble(current, "windspeedKmph"),
            WindDirection = ReadDouble(current, "winddirDegree"),
            Pressure = ReadDouble(current, "pressure"),
            Precipitation = ReadDouble(current, "precipMM"),
            IsDay = hour is >= 6 and < 18,
            TodayMin = todayMin,
            TodayMax = todayMax,
            HasDailyRange = hasRange
        };
    }

    /// <summary>把 WWO 天气编号归一到通用天气现象。</summary>
    private static WeatherCondition MapCode(int code) => code switch
    {
        113 => WeatherCondition.Clear,
        116 => WeatherCondition.PartlyCloudy,
        119 => WeatherCondition.Cloudy,
        122 => WeatherCondition.Overcast,
        143 or 248 or 260 => WeatherCondition.Fog,
        149 => WeatherCondition.Haze,
        200 => WeatherCondition.Thunderstorm,
        386 or 389 or 392 or 395 => WeatherCondition.Thunderstorm,
        // 冻雨、冻毛毛雨
        281 or 284 or 311 or 314 => WeatherCondition.Sleet,
        // 毛毛雨与小雨
        176 or 263 or 266 or 293 or 296 or 353 => WeatherCondition.Rain,
        // 中到大雨、强阵雨
        299 or 302 or 305 or 308 or 356 or 359 => WeatherCondition.HeavyRain,
        // 雨夹雪类
        179 or 182 or 185 or 317 or 320 or 362 or 365 => WeatherCondition.Sleet,
        // 各类降雪
        227 or 230 or 323 or 326 or 329 or 332 or 335 or 338 or 350 or 368 or 371 or 374 or 377 => WeatherCondition.Snow,
        _ => WeatherCondition.Unknown
    };

    /// <summary>取 wttr.in 给的英文天气描述，例如 "Smoky haze"。</summary>
    private static string ReadDescription(JsonElement current)
    {
        if (!current.TryGetProperty("weatherDesc", out var array) ||
            array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        return array[0].TryGetProperty("value", out var value) ? value.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>按英文描述兜底判断天气现象。</summary>
    private static WeatherCondition MapDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return WeatherCondition.Unknown;
        }

        var d = description.ToLowerInvariant();
        if (d.Contains("thunder")) return WeatherCondition.Thunderstorm;
        if (d.Contains("blizzard")) return WeatherCondition.Snow;
        if (d.Contains("heavy rain")) return WeatherCondition.HeavyRain;
        if (d.Contains("freezing") || d.Contains("sleet")) return WeatherCondition.Sleet;
        if (d.Contains("snow")) return WeatherCondition.Snow;
        if (d.Contains("drizzle")) return WeatherCondition.Drizzle;
        if (d.Contains("rain") || d.Contains("shower")) return WeatherCondition.Rain;
        if (d.Contains("haze") || d.Contains("smoky") || d.Contains("dust") || d.Contains("sand")) return WeatherCondition.Haze;
        if (d.Contains("mist") || d.Contains("fog")) return WeatherCondition.Fog;
        if (d.Contains("overcast")) return WeatherCondition.Overcast;
        if (d.Contains("cloudy")) return WeatherCondition.Cloudy;
        if (d.Contains("sunny") || d.Contains("clear")) return WeatherCondition.Clear;
        return WeatherCondition.Unknown;
    }

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;

    private static int ParseInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) &&
        int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}
