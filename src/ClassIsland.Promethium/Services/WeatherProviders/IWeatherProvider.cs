// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>一次天气查询要传的东西。</summary>
/// <param name="Latitude">纬度。</param>
/// <param name="Longitude">经度。</param>
/// <param name="ApiKey">需要密钥的数据源用，免密钥的忽略。</param>
public record WeatherQuery(double Latitude, double Longitude, string ApiKey = "");

/// <summary>一个天气数据源。</summary>
public interface IWeatherProvider
{
    /// <summary>数据源标识。</summary>
    WeatherProviderKind Kind { get; }

    /// <summary>界面上显示的名字。</summary>
    string DisplayName { get; }

    /// <summary>是否需要用户自己填密钥。</summary>
    bool RequiresApiKey { get; }

    /// <summary>查一次当前天气。</summary>
    Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default);
}
