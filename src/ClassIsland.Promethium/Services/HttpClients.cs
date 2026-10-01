// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Net.Http;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 所有对外请求共用的 HttpClient。
/// </summary>
/// <remarks>
/// 共用一个实例是为了别把连接池开一堆；User-Agent 统一带上，
/// 这是各家公开接口的基本要求（也是出问题时人家能找回来的唯一线索）。
/// </remarks>
public static class HttpClients
{
    private const string UserAgent =
        "ClassIsland-Promethium/1.0 (+https://github.com/QiuXiYueShiJiu/ClassIslandPromethium)";

    private static readonly Lazy<HttpClient> LazyClient = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.5");
        return client;
    });

    /// <summary>取共用实例。</summary>
    public static HttpClient Shared => LazyClient.Value;
}
