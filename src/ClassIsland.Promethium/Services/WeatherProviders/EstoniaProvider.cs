// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Xml.Linq;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 爱沙尼亚气象局的实时观测。
/// </summary>
/// <remarks>
/// 它一次返回全国所有站点（带经纬度），所以做法是拉回来挑离你最近的那个。
/// 观测站是稀疏的，站点距离可能几十公里——这一点在数据源说明里写明了。
/// <para/>
/// 单位已在实测中核过：风速是<b>米每秒</b>（这里换算成公里每小时），
/// 气压 hPa，风向角度，气温摄氏度，降水毫米。
/// </remarks>
public class EstoniaProvider : IWeatherProvider
{
    private const string Endpoint = "https://www.ilmateenistus.ee/ilma_andmed/xml/observations.php";

    /// <summary>
    /// 最近站点超过这个距离就认为坐标不在覆盖范围内。
    /// </summary>
    /// <remarks>
    /// 和 NEA 同理：不自己判距离的话，境外坐标也会拿到爱沙尼亚的天气，
    /// 而且从界面上完全看不出来。爱沙尼亚全境南北约 250 公里，
    /// 国内任意点离最近站都不该超过 200 公里。
    /// </remarks>
    private const double MaxStationDistanceKm = 200d;

    public WeatherProviderKind Kind => WeatherProviderKind.Estonia;

    public string DisplayName => "爱沙尼亚";

    public bool RequiresApiKey => false;

    public async Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default)
    {
        var xml = await HttpClients.Shared.GetStringAsync(Endpoint, token).ConfigureAwait(false);
        var root = XDocument.Parse(xml).Root
                   ?? throw new InvalidOperationException("爱沙尼亚返回的 XML 是空的");

        XElement? best = null;
        var bestDistance = double.MaxValue;
        foreach (var station in root.Elements("station"))
        {
            // 没有气温的站点说明当前无有效观测，跳过
            if (string.IsNullOrWhiteSpace(Value(station, "airtemperature")))
            {
                continue;
            }

            if (!GeoDistance.TryParseCoordinate(Value(station, "latitude"), out var latitude) ||
                !GeoDistance.TryParseCoordinate(Value(station, "longitude"), out var longitude))
            {
                continue;
            }

            var distance = GeoDistance.HaversineKm(query.Latitude, query.Longitude, latitude, longitude);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = station;
            }
        }

        if (best == null)
        {
            throw new InvalidOperationException("爱沙尼亚没有返回任何有效的观测站数据");
        }

        if (bestDistance > MaxStationDistanceKm)
        {
            throw new InvalidOperationException(
                $"该坐标不在爱沙尼亚覆盖范围内（最近的观测站在 {bestDistance:0} 公里外），请换用别的数据源");
        }

        var temperature = Parse(Value(best, "airtemperature"));
        var observedAt = long.TryParse(root.Attribute("timestamp")?.Value, out var timestamp)
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime()
            : DateTimeOffset.Now;
        var phenomenon = Value(best, "phenomenon");

        return new WeatherSnapshot
        {
            ProviderName = $"{DisplayName}（{Value(best, "name")}，约 {bestDistance:0} 公里外）",
            ObservedAt = observedAt,
            RawCode = phenomenon,
            Condition = WeatherConditionText.FromEnglish(phenomenon),
            Temperature = temperature,
            // 观测不给体感温度，用气温顶上
            FeelsLike = temperature,
            Humidity = Parse(Value(best, "relativehumidity")),
            // 观测给的是 m/s
            WindSpeed = Parse(Value(best, "windspeed")) * 3.6,
            WindDirection = Parse(Value(best, "winddirection"), -1d),
            Pressure = Parse(Value(best, "airpressure")),
            Precipitation = Parse(Value(best, "precipitations")),
            IsDay = DateTime.Now.Hour is >= 6 and < 18,
            HasDailyRange = false
        };
    }

    private static string Value(XElement station, string name) => station.Element(name)?.Value ?? string.Empty;

    private static double Parse(string text, double fallback = 0d) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
