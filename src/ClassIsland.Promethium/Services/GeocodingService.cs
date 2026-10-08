// Pm钷 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>一个地名候选项。</summary>
/// <param name="DisplayName">完整地址。</param>
/// <param name="Latitude">纬度（WGS84）。</param>
/// <param name="Longitude">经度（WGS84）。</param>
public record PlaceCandidate(string DisplayName, double Latitude, double Longitude);

/// <summary>一次反查的结果。</summary>
/// <param name="Name">地点名。</param>
/// <param name="ProviderName">是哪一家给的，显示给用户看以便排查。</param>
public record GeocodeResult(string Name, string ProviderName);

/// <summary>
/// 经纬度与地名互查，带自动降级。
/// </summary>
/// <remarks>
/// 这是「地名反查不成功」那个问题的修法。原来只挂了 Nominatim 一家，
/// 而它在国内经常连不上——一家不通就整个功能不可用。
/// 现在按顺序试多家，谁能用用谁，并且把实际用的是哪一家显示出来，
/// 下次再出问题能一眼看出是网络还是某一家自己的毛病。
/// <para/>
/// Nominatim 的公共实例有使用条款：必须带能识别来源的 User-Agent，
/// 而且每秒最多一次请求。这里用节流闸把并发压住，并且只在用户明确操作时调用。
/// </remarks>
public class GeocodingService
{
    private const string NominatimEndpoint = "https://nominatim.openstreetmap.org";
    private const string PhotonEndpoint = "https://photon.komoot.io";
    private const string BigDataCloudEndpoint = "https://api.bigdatacloud.net/data/reverse-geocode-client";
    private const string AMapEndpoint = "https://restapi.amap.com/v3";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastRequest = DateTime.MinValue;

    private readonly PromethiumConfigStore _store;

    public GeocodingService(PromethiumConfigStore store)
    {
        _store = store;
    }

    /// <summary>反查的一串候选，按顺序试。高德只在填了密钥时才排到最前面。</summary>
    private IReadOnlyList<GeocodingProviderKind> ReverseChain()
    {
        var config = _store.Weather;
        if (config.GeocodingProvider != GeocodingProviderKind.Auto)
        {
            return new[] { config.GeocodingProvider };
        }

        var chain = new List<GeocodingProviderKind>();
        if (!string.IsNullOrWhiteSpace(config.AmapGeocodingKey))
        {
            chain.Add(GeocodingProviderKind.AMap);
        }

        // 顺序有讲究：高德最贴国内；Nominatim 能到街道；
        // BigDataCloud 虽然粗一些但给的是规整中文行政区划（能到街道）；
        // Photon 能到门牌，但部分字段是拼音，对国内用户最不好用，所以放最后。
        chain.Add(GeocodingProviderKind.Nominatim);
        chain.Add(GeocodingProviderKind.BigDataCloud);
        chain.Add(GeocodingProviderKind.Photon);
        return chain;
    }

    /// <summary>
    /// 反查经纬度对应的地名。
    /// </summary>
    /// <exception cref="InvalidOperationException">所有候选都失败时抛出，消息里带上各家的失败原因。</exception>
    public async Task<GeocodeResult> ReverseAsync(double latitude, double longitude, CancellationToken token = default)
    {
        var reasons = new List<string>();

        foreach (var kind in ReverseChain())
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var name = kind switch
                {
                    GeocodingProviderKind.Nominatim => await NominatimReverseAsync(latitude, longitude, token).ConfigureAwait(false),
                    GeocodingProviderKind.Photon => await PhotonReverseAsync(latitude, longitude, token).ConfigureAwait(false),
                    GeocodingProviderKind.BigDataCloud => await BigDataCloudReverseAsync(latitude, longitude, token).ConfigureAwait(false),
                    GeocodingProviderKind.AMap => await AMapReverseAsync(latitude, longitude, token).ConfigureAwait(false),
                    _ => string.Empty
                };

                if (!string.IsNullOrWhiteSpace(name))
                {
                    return new GeocodeResult(name, Describe(kind));
                }

                reasons.Add($"{Describe(kind)}：没查到");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                reasons.Add($"{Describe(kind)}：{Explain(ex)}");
            }
        }

        throw new InvalidOperationException("地名服务都没能返回结果（" + string.Join("；", reasons) + "）");
    }

    /// <summary>按地名查候选点。</summary>
    public async Task<IReadOnlyList<PlaceCandidate>> SearchAsync(string query, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<PlaceCandidate>();
        }

        var reasons = new List<string>();
        foreach (var kind in ReverseChain().Where(k => k != GeocodingProviderKind.BigDataCloud))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var results = kind switch
                {
                    GeocodingProviderKind.Nominatim => await NominatimSearchAsync(query, token).ConfigureAwait(false),
                    GeocodingProviderKind.Photon => await PhotonSearchAsync(query, token).ConfigureAwait(false),
                    GeocodingProviderKind.AMap => await AMapSearchAsync(query, token).ConfigureAwait(false),
                    _ => Array.Empty<PlaceCandidate>()
                };

                if (results.Count > 0)
                {
                    return results;
                }

                reasons.Add($"{Describe(kind)}：没搜到");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                reasons.Add($"{Describe(kind)}：{Explain(ex)}");
            }
        }

        if (reasons.Count > 0)
        {
            throw new InvalidOperationException("地名搜索都没能返回结果（" + string.Join("；", reasons) + "）");
        }

        return Array.Empty<PlaceCandidate>();
    }

    // ---------------- 各家实现 ----------------

    private static async Task<string> NominatimReverseAsync(double latitude, double longitude, CancellationToken token)
    {
        await ThrottleAsync(token).ConfigureAwait(false);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/reverse?format=jsonv2&lat={1}&lon={2}&zoom=18&addressdetails=1", NominatimEndpoint, latitude, longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("address", out var address))
        {
            return JsonRead.Text(root, "display_name");
        }

        // 由粗到细拼：省 市 区 街道 路
        var parts = new[] { "state", "city", "district", "city_district", "suburb", "town", "county", "road", "neighbourhood" };
        var picked = new List<string>();
        foreach (var key in parts)
        {
            var text = JsonRead.Text(address, key);
            if (!string.IsNullOrWhiteSpace(text) && !picked.Contains(text))
            {
                picked.Add(text);
            }
        }

        return picked.Count > 0 ? string.Concat(picked) : JsonRead.Text(root, "display_name");
    }

    private static async Task<IReadOnlyList<PlaceCandidate>> NominatimSearchAsync(string query, CancellationToken token)
    {
        await ThrottleAsync(token).ConfigureAwait(false);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/search?format=jsonv2&limit=6&addressdetails=0&q={1}", NominatimEndpoint, Uri.EscapeDataString(query));

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);

        var results = new List<PlaceCandidate>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var latitude = ParseDouble(JsonRead.Text(item, "lat"));
            var longitude = ParseDouble(JsonRead.Text(item, "lon"));
            if (latitude == 0d && longitude == 0d)
            {
                continue;
            }

            results.Add(new PlaceCandidate(JsonRead.Text(item, "display_name"), latitude, longitude));
        }

        return results;
    }

    private static async Task<string> PhotonReverseAsync(double latitude, double longitude, CancellationToken token)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/reverse?lat={1:0.######}&lon={2:0.######}", PhotonEndpoint, latitude, longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("features", out var features) || features.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var properties = features[0].GetProperty("properties");
        // 由粗到细：省 市 区 社区 路 门牌
        var parts = new[] { "state", "city", "district", "locality", "street", "housenumber", "name" };
        var picked = new List<string>();
        foreach (var key in parts)
        {
            var text = JsonRead.Text(properties, key);
            if (!string.IsNullOrWhiteSpace(text) && !picked.Contains(text))
            {
                picked.Add(text);
            }
        }

        return string.Concat(picked);
    }

    private static async Task<IReadOnlyList<PlaceCandidate>> PhotonSearchAsync(string query, CancellationToken token)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/api/?limit=6&q={1}", PhotonEndpoint, Uri.EscapeDataString(query));

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);

        var results = new List<PlaceCandidate>();
        if (!document.RootElement.TryGetProperty("features", out var features))
        {
            return results;
        }

        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("geometry", out var geometry) ||
                !geometry.TryGetProperty("coordinates", out var coordinates) ||
                coordinates.ValueKind != JsonValueKind.Array || coordinates.GetArrayLength() < 2)
            {
                continue;
            }

            var properties = feature.GetProperty("properties");
            var parts = new[] { "name", "street", "housenumber", "district", "city", "state", "country" };
            var picked = new List<string>();
            foreach (var key in parts)
            {
                var text = JsonRead.Text(properties, key);
                if (!string.IsNullOrWhiteSpace(text) && !picked.Contains(text))
                {
                    picked.Add(text);
                }
            }

            // Photon 的坐标顺序是 [经度, 纬度]
            results.Add(new PlaceCandidate(string.Join(" ", picked), coordinates[1].GetDouble(), coordinates[0].GetDouble()));
        }

        return results;
    }

    private static async Task<string> BigDataCloudReverseAsync(double latitude, double longitude, CancellationToken token)
    {
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}?latitude={1:0.######}&longitude={2:0.######}&localityLanguage=zh",
            BigDataCloudEndpoint, latitude, longitude);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // 行政区划层级由粗到细，最后几级能到区和街道
        if (root.TryGetProperty("localityInfo", out var info) &&
            info.TryGetProperty("administrative", out var administrative))
        {
            var names = new List<string>();
            foreach (var level in administrative.EnumerateArray())
            {
                var name = JsonRead.Text(level, "name");
                if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                {
                    names.Add(name);
                }
            }

            if (names.Count > 0)
            {
                return string.Concat(names);
            }
        }

        return string.Concat(JsonRead.Text(root, "countryName"), JsonRead.Text(root, "principalSubdivision"),
            JsonRead.Text(root, "city"), JsonRead.Text(root, "locality"));
    }

    private async Task<string> AMapReverseAsync(double latitude, double longitude, CancellationToken token)
    {
        var key = _store.Weather.AmapGeocodingKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        // 高德吃的是 GCJ-02 的「经度,纬度」
        var (gcjLat, gcjLon) = ChinaCoordinate.Wgs84ToGcj02(latitude, longitude);
        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/geocode/regeo?location={1:0.######},{2:0.######}&key={3}&radius=1000&extensions=base",
            AMapEndpoint, gcjLon, gcjLat, key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (JsonRead.Text(root, "status") != "1")
        {
            throw new InvalidOperationException("高德返回 " + JsonRead.Text(root, "info"));
        }

        if (!root.TryGetProperty("regeocode", out var regeocode))
        {
            return string.Empty;
        }

        var formatted = JsonRead.Text(regeocode, "formatted_address");
        if (!string.IsNullOrWhiteSpace(formatted))
        {
            return formatted;
        }

        if (regeocode.TryGetProperty("addressComponent", out var component))
        {
            var street = JsonRead.NestedText(component, "streetNumber", "street");
            return string.Concat(JsonRead.Text(component, "province"), JsonRead.Text(component, "city"),
                JsonRead.Text(component, "district"), JsonRead.Text(component, "township"), street);
        }

        return string.Empty;
    }

    private async Task<IReadOnlyList<PlaceCandidate>> AMapSearchAsync(string query, CancellationToken token)
    {
        var key = _store.Weather.AmapGeocodingKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<PlaceCandidate>();
        }

        var url = string.Format(CultureInfo.InvariantCulture,
            "{0}/place/text?keywords={1}&key={2}&offset=6&page=1", AMapEndpoint, Uri.EscapeDataString(query), key);

        var json = await HttpClients.Shared.GetStringAsync(url, token).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (JsonRead.Text(root, "status") != "1")
        {
            throw new InvalidOperationException("高德返回 " + JsonRead.Text(root, "info"));
        }

        var results = new List<PlaceCandidate>();
        if (!root.TryGetProperty("pois", out var pois))
        {
            return results;
        }

        foreach (var poi in pois.EnumerateArray())
        {
            // 高德给的是 GCJ-02 的「经度,纬度」
            var location = JsonRead.Text(poi, "location").Split(',');
            if (location.Length != 2 ||
                !double.TryParse(location[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLon) ||
                !double.TryParse(location[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var gcjLat))
            {
                continue;
            }

            var (latitude, longitude) = ChinaCoordinate.Gcj02ToWgs84(gcjLat, gcjLon);
            var name = JsonRead.Text(poi, "name");
            var address = JsonRead.Text(poi, "address");
            results.Add(new PlaceCandidate(string.IsNullOrEmpty(address) ? name : $"{address} {name}", latitude, longitude));
        }

        return results;
    }

    // ---------------- 公共设施 ----------------

    /// <summary>把两次请求之间压到 1 秒以上（Nominatim 的要求）。</summary>
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

    private static string Describe(GeocodingProviderKind kind) => kind switch
    {
        GeocodingProviderKind.Nominatim => "Nominatim",
        GeocodingProviderKind.Photon => "Photon",
        GeocodingProviderKind.BigDataCloud => "BigDataCloud",
        GeocodingProviderKind.AMap => "高德",
        _ => "自动"
    };

    /// <summary>把异常翻译成一句能帮上忙的话。</summary>
    private static string Explain(Exception ex) => ex switch
    {
        TaskCanceledException => "超时",
        HttpRequestException http => $"连不上（HTTP {(int?)http.StatusCode}）",
        _ => ex.GetType().Name
    };

    private static double ParseDouble(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;
}
