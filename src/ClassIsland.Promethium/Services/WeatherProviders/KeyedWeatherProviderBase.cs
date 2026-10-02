// Pm钷 —— ClassIsland 综合增强插件
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services.WeatherProviders;

/// <summary>
/// 需要密钥的数据源的共同部分。
/// </summary>
/// <remarks>
/// 主要就一件事：没填密钥时给一句人看得懂的话，而不是让它去请求、
/// 然后甩一个 HTTP 401 出来——401 对用户来说等于没说。
/// </remarks>
public abstract class KeyedWeatherProviderBase : IWeatherProvider
{
    /// <inheritdoc />
    public abstract WeatherProviderKind Kind { get; }

    /// <inheritdoc />
    public abstract string DisplayName { get; }

    /// <inheritdoc />
    public bool RequiresApiKey => true;

    /// <inheritdoc />
    public abstract Task<WeatherSnapshot> QueryAsync(WeatherQuery query, CancellationToken token = default);

    /// <summary>取密钥；没填就抛一句能看懂的异常。</summary>
    protected string RequireApiKey(WeatherQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.ApiKey))
        {
            throw new InvalidOperationException($"{DisplayName} 需要 API 密钥，请先在设置里填写");
        }

        return query.ApiKey.Trim();
    }

    /// <summary>按当地时刻粗略判断白天还是夜间，给那些不提供这个信息的数据源兜底。</summary>
    protected static bool GuessIsDay() => DateTime.Now.Hour is >= 6 and < 18;
}
