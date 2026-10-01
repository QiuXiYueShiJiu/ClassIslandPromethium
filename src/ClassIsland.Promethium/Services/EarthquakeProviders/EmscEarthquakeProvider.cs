// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.EarthquakeProviders;

/// <summary>
/// 欧洲-地中海地震中心（EMSC）公开目录。
/// </summary>
/// <remarks>
/// 两个必须注意的地方：
/// <list type="number">
/// <item>它<b>不接受</b> <c>maxradiuskm</c>，会直接返回 400 说参数不认识。
///       所以这里改成用经纬度包围盒去框，框完再用大圆距离自己筛一遍。</item>
/// <item>它的 <c>geometry.coordinates[2]</c> 深度是负的（-10 表示地下 10 公里），
///       而 <c>properties.depth</c> 是正的。这里用后者，免得显示出负深度。</item>
/// </list>
/// </remarks>
public class EmscEarthquakeProvider : IEarthquakeProvider
{
    private const string Endpoint = "https://www.seismicportal.eu/fdsnws/event/1/query";

    public EarthquakeSource Source => EarthquakeSource.Emsc;

    public string DisplayName => "EMSC";

    public async Task<IReadOnlyList<EarthquakeEvent>> QueryRecentAsync(EarthquakeQuery query, CancellationToken token = default)
    {
        // 1 度纬度约 111.32 公里；经度还要乘 cos(纬度)。高纬度处 cos 接近 0，
        // 夹一下下限免得除爆，包围盒大一点无所谓，反正后面按真实距离再筛。
        const double kmPerDegreeLatitude = 111.32;
        var latitudeRadians = query.Latitude * Math.PI / 180.0;
        var kmPerDegreeLongitude = kmPerDegreeLatitude * Math.Max(Math.Cos(latitudeRadians), 0.05);

        var latitudeDelta = query.RadiusKm / kmPerDegreeLatitude;
        var longitudeDelta = query.RadiusKm / kmPerDegreeLongitude;

        var start = DateTime.UtcNow - TimeSpan.FromHours(Math.Clamp(query.LookbackHours, 1d, 720d));
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?format=json&minlatitude={1:0.####}&maxlatitude={2:0.####}" +
            "&minlongitude={3:0.####}&maxlongitude={4:0.####}&minmag={5:0.#}" +
            "&start={6}&orderby=time&limit=100",
            Endpoint,
            Math.Max(query.Latitude - latitudeDelta, -90d),
            Math.Min(query.Latitude + latitudeDelta, 90d),
            Math.Max(query.Longitude - longitudeDelta, -180d),
            Math.Min(query.Longitude + longitudeDelta, 180d),
            Math.Clamp(query.MinMagnitude, 0d, 10d),
            start.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);

        var results = new List<EarthquakeEvent>();
        if (!document.RootElement.TryGetProperty("features", out var features))
        {
            return results;
        }

        foreach (var feature in features.EnumerateArray())
        {
            var parsed = Parse(feature, query);
            if (parsed != null)
            {
                results.Add(parsed);
            }
        }

        return results;
    }

    private static EarthquakeEvent? Parse(JsonElement feature, EarthquakeQuery query)
    {
        try
        {
            if (!feature.TryGetProperty("properties", out var properties))
            {
                return null;
            }

            if (!properties.TryGetProperty("mag", out var magElement) || magElement.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var magnitude = magElement.GetDouble();
            var latitude = ReadDouble(properties, "lat");
            var longitude = ReadDouble(properties, "lon");
            // 用 properties.depth（正值），不用 geometry 里那个带符号的
            var depth = ReadDouble(properties, "depth");
            var distance = GeoMath.HaversineKm(query.Latitude, query.Longitude, latitude, longitude);

            // 包围盒是方的，这里按真实半径再筛一次
            if (distance > query.RadiusKm)
            {
                return null;
            }

            var time = properties.TryGetProperty("time", out var timeElement) &&
                       DateTimeOffset.TryParse(timeElement.GetString(), CultureInfo.InvariantCulture,
                           DateTimeStyles.AdjustToUniversal, out var parsed)
                ? parsed.ToLocalTime()
                : DateTimeOffset.Now;

            var id = properties.TryGetProperty("unid", out var unidElement)
                ? unidElement.GetString() ?? string.Empty
                : properties.TryGetProperty("source_id", out var sourceElement) ? sourceElement.GetString() ?? string.Empty : string.Empty;

            return new EarthquakeEvent
            {
                Id = id,
                Magnitude = magnitude,
                Place = properties.TryGetProperty("flynn_region", out var regionElement) ? regionElement.GetString() ?? string.Empty : string.Empty,
                Time = time,
                Latitude = latitude,
                Longitude = longitude,
                DepthKm = depth,
                Url = "https://www.seismicportal.eu/eventdetails.html?unid=" + Uri.EscapeDataString(id),
                DistanceKm = distance,
                Felt = GeoMath.JudgeFelt(magnitude, distance)
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static double ReadDouble(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0d;
}
