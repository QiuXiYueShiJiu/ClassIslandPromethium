// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 新加坡国家环境局（NEA）的实时观测。
/// </summary>
/// <remarks>
/// 它不提供「一次拿到全部要素」的接口，气温、湿度、风速、降雨各是一个端点，
/// 所以这里并发打四个请求再把结果拼起来。每个端点的站点集合还不完全一样，
/// 因此是**逐个端点各自找最近站**，而不是先定一个站再到处查它。
/// <para/>
/// 单位已实测核准：风速是<b>节</b>（换算成公里每小时），湿度百分比，降雨是 5 分钟累计毫米。
/// <para/>
/// <b>它没有天空状况字段</b>，所以天气现象只能靠降雨推断：有雨算有雨，
/// 没雨就是不详——不看着云图猜一个「多云」出来。
/// </remarks>
public class NeaSingaporeProvider : IWeatherProvider
{
    private const string BaseUrl = "https://api.data.gov.sg/v1/environment";

    /// <summary>一节等于 1.852 公里每小时。</summary>
    private const double KnotsToKmh = 1.852;

    /// <summary>
    /// 最近站点超过这个距离就认为坐标不在覆盖范围内。
    /// </summary>
    /// <remarks>
    /// 这个守卫是必须的：NEA 对任意经纬度都会正常返回新加坡的读数，
    /// 不自己判距离的话，北京用户选了它就会看到新加坡的天气，
    /// 而且界面上完全看不出哪里不对。宁可报错也不能给错数据。
    /// </remarks>
    private const double MaxStationDistanceKm = 120d;

    public WeatherProviderKind Kind => WeatherProviderKind.NeaSingapore;

    public string DisplayName => "NEA 新加坡";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var temperatureTask = FetchNearestAsync("air-temperature", query, token);
        var humidityTask = FetchNearestAsync("relative-humidity", query, token);
        var windTask = FetchNearestAsync("wind-speed", query, token);
        var rainTask = FetchNearestAsync("rainfall", query, token);

        await Task.WhenAll(temperatureTask, humidityTask, windTask, rainTask).ConfigureAwait(false);

        var temperature = temperatureTask.Result;
        if (temperature == null)
        {
            throw new InvalidOperationException("NEA 没有返回有效读数");
        }

        if (temperature.DistanceKm > MaxStationDistanceKm)
        {
            throw new InvalidOperationException(
                $"该坐标不在 NEA 覆盖范围内（只提供新加坡数据，最近的观测站在 {temperature.DistanceKm:0} 公里外），请换用别的数据源");
        }

        var windKnots = windTask.Result?.Value ?? 0d;
        var rainfall = rainTask.Result?.Value ?? 0d;
        var humidity = humidityTask.Result?.Value ?? 0d;

        return new WeatherSnapshot
        {
            ProviderName = $"{DisplayName}（{temperature.StationName}）",
            ObservedAt = temperature.Time,
            RawCode = rainfall > 0 ? "rain" : "no-rain",
            // NEA 不给天空状况，只有降雨能推断
            Condition = rainfall > 0 ? WeatherCondition.Rain : WeatherCondition.Unknown,
            Temperature = temperature.Value,
            // 观测不给体感温度，用气温顶上
            FeelsLike = temperature.Value,
            Humidity = humidity,
            WindSpeed = windKnots * KnotsToKmh,
            // NEA 不提供风向
            WindDirection = -1d,
            // NEA 不提供气压
            Pressure = 0d,
            Precipitation = rainfall,
            IsDay = DateTime.Now.Hour is >= 6 and < 18,
            HasDailyRange = false
        };
    }

    private static async Task<NeaReading?> FetchNearestAsync(string endpoint, WeatherQuery query, CancellationToken token)
    {
        var json = await HttpClients.Shared.GetStringAsync($"{BaseUrl}/{endpoint}", token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("metadata", out var metadata) ||
            !metadata.TryGetProperty("stations", out var stations) ||
            !root.TryGetProperty("items", out var items) || items.GetArrayLength() == 0 ||
            !items[0].TryGetProperty("readings", out var readings))
        {
            return null;
        }

        // 站点号 -> 读数
        var readingByStation = new Dictionary<string, double>();
        foreach (var reading in readings.EnumerateArray())
        {
            var id = JsonRead.Text(reading, "station_id");
            if (!string.IsNullOrEmpty(id))
            {
                readingByStation[id] = JsonRead.Number(reading, "value");
            }
        }

        string? bestId = null;
        string bestName = string.Empty;
        var bestDistance = double.MaxValue;
        foreach (var station in stations.EnumerateArray())
        {
            var id = JsonRead.Text(station, "id");
            if (string.IsNullOrEmpty(id) || !readingByStation.ContainsKey(id))
            {
                continue;
            }

            var latitude = JsonRead.Nested(station, "location", "latitude");
            var longitude = JsonRead.Nested(station, "location", "longitude");
            if (latitude == 0d && longitude == 0d)
            {
                continue;
            }

            var distance = GeoDistance.HaversineKm(query.Latitude, query.Longitude, latitude, longitude);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = id;
                bestName = JsonRead.Text(station, "name");
            }
        }

        if (bestId == null)
        {
            return null;
        }

        var time = items[0].TryGetProperty("timestamp", out var timestamp) &&
                   DateTimeOffset.TryParse(timestamp.GetString(), CultureInfo.InvariantCulture,
                       DateTimeStyles.None, out var parsed)
            ? parsed.ToLocalTime()
            : DateTimeOffset.Now;

        return new NeaReading(readingByStation[bestId], bestName, time, bestDistance);
    }

    private record NeaReading(double Value, string StationName, DateTimeOffset Time, double DistanceKm);
}
