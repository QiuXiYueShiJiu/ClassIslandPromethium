// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace ClassIsland.Promethium.Services;

/// <summary>一个地名候选项。</summary>
/// <param name="DisplayName">完整地址。</param>
/// <param name="Latitude">纬度。</param>
/// <param name="Longitude">经度。</param>
public record PlaceCandidate(string DisplayName, double Latitude, double Longitude);

/// <summary>
/// 用 OpenStreetMap 的 Nominatim 做地理编码：经纬度反查地名、地名正查经纬度。
/// </summary>
/// <remarks>
/// Nominatim 的公共实例有使用条款：必须带能识别来源的 User-Agent，
/// 而且每秒最多一次请求。这里老实用一个节流闸把并发压住，
/// 并且只在用户明确操作时调用，不做轮询。
/// </remarks>
public class NominatimService
{
    private const string Endpoint = "https://nominatim.openstreetmap.org";

    private static HttpClient Http => HttpClients.Shared;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastRequest = DateTime.MinValue;

    /// <summary>按 Nominatim 的要求把请求间隔压到 1 秒以上。</summary>
    private static async Task ThrottleAsync(CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var since = DateTime.UtcNow - _lastRequest;
            if (since < TimeSpan.FromSeconds(1.1))
            {
                await Task.Delay(TimeSpan.FromSeconds(1.1) - since, token).ConfigureAwait(false);
            }
            _lastRequest = DateTime.UtcNow;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// 反查经纬度对应的地名。返回的字符串会尽量细到街道。
    /// </summary>
    public async Task<string> ReverseAsync(double latitude, double longitude, CancellationToken token = default)
    {
        await ThrottleAsync(token).ConfigureAwait(false);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/reverse?format=jsonv2&lat={1}&lon={2}&zoom=18&addressdetails=1", Endpoint, latitude, longitude);

        using var response = await Http.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("address", out var address))
        {
            return document.RootElement.TryGetProperty("display_name", out var name)
                ? name.GetString() ?? string.Empty
                : string.Empty;
        }

        // 由粗到细拼：省 市 区 街道 路
        var parts = new[] { "state", "city", "district", "city_district", "suburb", "town", "county", "road", "neighbourhood" };
        var picked = new List<string>();
        foreach (var key in parts)
        {
            if (address.TryGetProperty(key, out var value))
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text) && !picked.Contains(text))
                {
                    picked.Add(text);
                }
            }
        }

        return picked.Count > 0 ? string.Concat(picked) : string.Empty;
    }

    /// <summary>按地名搜索候选点。</summary>
    public async Task<IReadOnlyList<PlaceCandidate>> SearchAsync(string query, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PlaceCandidate>();
        }

        await ThrottleAsync(token).ConfigureAwait(false);
        var url = "{0}/search?format=jsonv2&limit=6&addressdetails=0&q={1}".Replace("{0}", Endpoint)
            .Replace("{1}", Uri.EscapeDataString(query));

        using var response = await Http.GetAsync(url, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);

        using var document = JsonDocument.Parse(json);
        var results = new List<PlaceCandidate>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("lat", out var lat) || !item.TryGetProperty("lon", out var lon))
            {
                continue;
            }
            if (!double.TryParse(lat.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                !double.TryParse(lon.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude))
            {
                continue;
            }
            var display = item.TryGetProperty("display_name", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            results.Add(new PlaceCandidate(display, latitude, longitude));
        }

        return results;
    }
}
