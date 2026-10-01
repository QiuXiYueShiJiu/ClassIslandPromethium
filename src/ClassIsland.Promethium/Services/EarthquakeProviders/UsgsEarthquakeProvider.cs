// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.EarthquakeProviders;

/// <summary>
/// USGS 公开地震目录。
/// </summary>
/// <remarks>
/// 接口原生支持按公里半径和最低震级筛选，返回 GeoJSON。
/// 注意它的 <c>metadata</c> 里没有记录条数字段，别去读 <c>count</c>。
/// </remarks>
public class UsgsEarthquakeProvider : IEarthquakeProvider
{
    private const string Endpoint = "https://earthquake.usgs.gov/fdsnws/event/1/query";

    public EarthquakeSource Source => EarthquakeSource.Usgs;

    public string DisplayName => "USGS";

    public async Task<IReadOnlyList<EarthquakeEvent>> QueryRecentAsync(EarthquakeQuery query, CancellationToken token = default)
    {
        var start = DateTime.UtcNow - TimeSpan.FromHours(Math.Clamp(query.LookbackHours, 1d, 720d));
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?format=geojson&latitude={1}&longitude={2}&maxradiuskm={3}&minmagnitude={4}" +
            "&starttime={5}&orderby=time&limit=100",
            Endpoint, query.Latitude, query.Longitude,
            Math.Clamp(query.RadiusKm, 1d, 20000d),
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
            var properties = feature.GetProperty("properties");
            var coordinates = feature.GetProperty("geometry").GetProperty("coordinates");

            if (!properties.TryGetProperty("mag", out var magElement) || magElement.ValueKind != JsonValueKind.Number)
            {
                return null;
            }

            var magnitude = magElement.GetDouble();
            var latitude = coordinates[1].GetDouble();
            var longitude = coordinates[0].GetDouble();
            var depth = coordinates[2].GetDouble();
            var distance = GeoMath.HaversineKm(query.Latitude, query.Longitude, latitude, longitude);

            var time = properties.TryGetProperty("time", out var timeElement) && timeElement.ValueKind == JsonValueKind.Number
                ? DateTimeOffset.FromUnixTimeMilliseconds(timeElement.GetInt64()).ToLocalTime()
                : DateTimeOffset.Now;

            return new EarthquakeEvent
            {
                Id = feature.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty,
                Magnitude = magnitude,
                Place = properties.TryGetProperty("place", out var placeElement) ? placeElement.GetString() ?? string.Empty : string.Empty,
                Time = time,
                Latitude = latitude,
                Longitude = longitude,
                DepthKm = depth,
                Url = properties.TryGetProperty("url", out var urlElement) ? urlElement.GetString() ?? string.Empty : string.Empty,
                DistanceKm = distance,
                Felt = GeoMath.JudgeFelt(magnitude, distance)
            };
        }
        catch (Exception)
        {
            // 单条结构不对就跳过，别让整次查询废掉
            return null;
        }
    }
}
