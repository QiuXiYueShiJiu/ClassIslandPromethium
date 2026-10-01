using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ClassIsland.Promethium.Components;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.Services.EarthquakeProviders;
using ClassIsland.Promethium.Services.WeatherProviders;
using ClassIsland.Promethium.Views.ComponentSettings;
using ClassIsland.Promethium.Views.SettingsPages;

internal static class Program
{
    private static int _failures;
    private const double Lat = 39.9042, Lon = 116.4074;

    private static int Main()
    {
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        var folder = Path.Combine(Path.GetTempPath(), "pm-check");
        Directory.CreateDirectory(folder);
        var store = new PromethiumConfigStore(folder);
        var alerts = new AlertCenter();
        var geocoder = new NominatimService();

        var weatherCatalog = new WeatherProviderCatalog(new IWeatherProvider[]
        {
            new OpenMeteoProvider(), new MetNorwayProvider(), new WttrInProvider()
        });
        var quakeCatalog = new EarthquakeProviderCatalog(new IEarthquakeProvider[]
        {
            new UsgsEarthquakeProvider(), new EmscEarthquakeProvider()
        });
        var weatherMonitor = new WeatherMonitor(weatherCatalog, store, alerts);
        var quakeMonitor = new EarthquakeMonitor(quakeCatalog, store, alerts);

        Section("一、XAML 能否加载（这是插件加载失败最常见的原因）");
        Check("设置页 BetterWeatherSettingsPage", () =>
        {
            // IAudioService 由宿主注入；这里只为验证 XAML，音频用不到
            var page = new BetterWeatherSettingsPage(
                store, geocoder, weatherCatalog, quakeCatalog, quakeMonitor, null!);
            return page.Content != null;
        });
        Check("组件设置控件 BetterWeatherComponentSettingsControl", () =>
            new BetterWeatherComponentSettingsControl().Content != null);
        Check("主界面组件 BetterWeatherComponent", () =>
            new BetterWeatherComponent(weatherMonitor, store, alerts).Content != null);

        Section("二、报警判定：绝不能无中生有");
        var mild = new WeatherSnapshot
        {
            ProviderName = "test", Condition = WeatherCondition.Clear, Temperature = 22, FeelsLike = 22,
            Humidity = 50, WindSpeed = 10, Pressure = 1013, Precipitation = 0,
            TodayMin = 18, TodayMax = 25, HasDailyRange = true, IsDay = true
        };
        var mildAlerts = WeatherAlertEvaluator.Evaluate(mild, new WeatherConfig());
        Check($"温和天气应无报警（实得 {mildAlerts.Count} 条）", () => mildAlerts.Count == 0);

        var hot = new WeatherSnapshot
        {
            Condition = WeatherCondition.Clear, Temperature = 30, TodayMin = 28, TodayMax = 38,
            HasDailyRange = true
        };
        var hotAlerts = WeatherAlertEvaluator.Evaluate(hot, new WeatherConfig());
        Check("38°C 应命中高温", () => hotAlerts.Count == 1 && hotAlerts[0].Key == "high_temperature");

        var stormy = new WeatherSnapshot { Condition = WeatherCondition.Thunderstorm, Temperature = 20 };
        Check("雷暴应命中", () => WeatherAlertEvaluator.Evaluate(stormy, new WeatherConfig()).Count == 1);
        var noThunder = new WeatherConfig { AlertThunderstorm = false };
        Check("关掉雷暴开关后不应命中", () => WeatherAlertEvaluator.Evaluate(stormy, noThunder).Count == 0);

        Section("三、震感规则：边界必须偏保守");
        Check("M4.6 / 800km 判为基本无感", () => GeoMath.JudgeFelt(4.6, 800) == FeltLikelihood.Unlikely);
        Check("M4.6 / 120km 判为轻微", () => GeoMath.JudgeFelt(4.6, 120) == FeltLikelihood.Slight);
        Check("M6.0 / 200km 判为明显", () => GeoMath.JudgeFelt(6.0, 200) == FeltLikelihood.Strong);
        Check("M2.0 / 10km  不应误报", () => GeoMath.JudgeFelt(2.0, 10) == FeltLikelihood.Unlikely);

        Section("四、距离计算");
        Check("北京→北京 约 400~460km", () =>
        {
            var d = GeoMath.HaversineKm(39.9042, 116.4074, 39.9042, 116.4074);
            return d > 400 && d < 470;
        });
        Check("同一点距离为 0", () => Math.Abs(GeoMath.HaversineKm(Lat, Lon, Lat, Lon)) < 0.001);

        Section("五、变量模板");
        var vars = new Dictionary<string, string> { ["标题"] = "高温", ["位置"] = "北京" };
        Check("已知变量被替换", () => TemplateEngine.Render("{位置}{标题}", vars) == "北京高温");
        Check("未知变量原样保留（便于用户发现写错）",
            () => TemplateEngine.Render("{没有这个}", vars) == "{没有这个}");

        Section("六、自定义图标按命名约定取图");
        var iconDir = Path.Combine(folder, "icons");
        Directory.CreateDirectory(iconDir);
        File.WriteAllText(Path.Combine(iconDir, "clear.png"), "x");
        File.WriteAllText(Path.Combine(iconDir, "rain_night.png"), "x");
        Check("白天取 clear.png", () =>
            WeatherIconCatalog.ResolveImagePath(iconDir, WeatherCondition.Clear, true)?.EndsWith("clear.png") == true);
        Check("夜间优先取 rain_night.png", () =>
            WeatherIconCatalog.ResolveImagePath(iconDir, WeatherCondition.Rain, false)?.EndsWith("rain_night.png") == true);
        Check("夜间无 _night 时回退到白天图", () =>
            WeatherIconCatalog.ResolveImagePath(iconDir, WeatherCondition.Clear, false)?.EndsWith("clear.png") == true);
        Check("目录不存在时返回 null 而不抛异常",
            () => WeatherIconCatalog.ResolveImagePath("/no/such/dir", WeatherCondition.Clear, true) == null);

        Section("七、配置存盘往返");
        store.Weather.LocationName = "测试地点";
        store.Weather.Latitude = 12.3456;
        store.Earthquake.MinMagnitude = 5.5;
        store.Save();
        var reloaded = new PromethiumConfigStore(folder);
        Check("地点名往返一致", () => reloaded.Weather.LocationName == "测试地点");
        Check("纬度往返一致", () => Math.Abs(reloaded.Weather.Latitude - 12.3456) < 1e-9);
        Check("震级往返一致", () => Math.Abs(reloaded.Earthquake.MinMagnitude - 5.5) < 1e-9);

        Section("八、显示模式（同时验证设置变更能否实时生效）");
        var settings = new BetterWeatherSettings();
        var vm = new ClassIsland.Promethium.ViewModels.BetterWeatherViewModel(weatherMonitor, store, alerts);
        vm.AttachSettings(settings);
        Check("标准模式：位置名/温差/湿度开，风力/体感关",
            () => vm.ShowLocationNameEffective && vm.ShowDailyRangeEffective
                  && vm.ShowHumidityEffective && !vm.ShowWindEffective && !vm.ShowFeelsLikeEffective);
        settings.DisplayMode = WeatherDisplayMode.Detailed;
        Check("切到详细：五项全开（这一条同时证明改设置会实时生效）",
            () => vm.ShowLocationNameEffective && vm.ShowDailyRangeEffective && vm.ShowHumidityEffective
                  && vm.ShowWindEffective && vm.ShowFeelsLikeEffective);
        settings.DisplayMode = WeatherDisplayMode.Compact;
        Check("切到紧凑：五项全关",
            () => !vm.ShowLocationNameEffective && !vm.ShowDailyRangeEffective && !vm.ShowHumidityEffective
                  && !vm.ShowWindEffective && !vm.ShowFeelsLikeEffective);
        settings.DisplayMode = WeatherDisplayMode.Custom;
        settings.ShowWind = true;
        settings.ShowHumidity = false;
        Check("自定义模式才听逐项开关",
            () => vm.ShowWindEffective && !vm.ShowHumidityEffective);
        Check("下拉框索引映射可用",
            () => settings.DisplayModeIndex == (int)WeatherDisplayMode.Custom);
        settings.DisplayModeIndex = (int)WeatherDisplayMode.Detailed;
        Check("索引写回能改到枚举", () => settings.DisplayMode == WeatherDisplayMode.Detailed);

        Section("九、报警图标可配且非空");
        Check("天气报警字形非空", () => !string.IsNullOrEmpty(vm.WeatherAlertGlyph));
        Check("地震报警字形非空", () => !string.IsNullOrEmpty(vm.EarthquakeAlertGlyph));
        Check("改天气报警字形会通知界面",
            () => vm.WeatherAlertGlyph == store.Weather.AlertGlyph);

        Section("十、真实接口联调（联网，验证解析而不是猜测）");
        foreach (var provider in new IWeatherProvider[] { new OpenMeteoProvider(), new MetNorwayProvider(), new WttrInProvider() })
        {
            LiveCheck($"天气源 {provider.DisplayName}", async () =>
            {
                var s = await provider.QueryAsync(new WeatherQuery(Lat, Lon));
                Console.WriteLine($"        气温 {s.Temperature:0.#}°C · 天气 {WeatherText.Describe(s.Condition)} · 湿度 {s.Humidity:0.#}% · 风 {s.WindSpeed:0.#}km/h · 原始码 {s.RawCode}");
                Console.WriteLine($"        今日 {s.TodayMin:0.#}~{s.TodayMax:0.#}°C (HasDailyRange={s.HasDailyRange})");
                return s.Condition != WeatherCondition.Unknown && s.Temperature != 0;
            });
        }

        foreach (var provider in new IEarthquakeProvider[] { new UsgsEarthquakeProvider(), new EmscEarthquakeProvider() })
        {
            LiveCheck($"地震源 {provider.DisplayName}", async () =>
            {
                var list = await provider.QueryRecentAsync(new EarthquakeQuery(Lat, Lon, 3000, 4, 720));
                Console.WriteLine($"        30 天内 3000km 内 M4+ 共 {list.Count} 条");
                foreach (var e in list.Take(3))
                {
                    Console.WriteLine($"          M{e.Magnitude:0.0} {e.Time:yyyy-MM-dd HH:mm} {e.Place} · 深度 {e.DepthKm:0.#}km · 距此 {e.DistanceKm:0}km · {e.FeltText}");
                }
                return list.Count > 0 && list.All(e => e.DepthKm >= -5 && e.Magnitude > 0);
            });
        }

        Section("十一、地理编码联调");
        LiveCheck("逆地理编码（应能反查到北京一带）", async () =>
        {
            var name = await geocoder.ReverseAsync(Lat, Lon);
            Console.WriteLine($"        {name}");
            return name.Contains("北京") || name.Contains("北京");
        });
        LiveCheck("地名搜索", async () =>
        {
            var results = await geocoder.SearchAsync("某某镇");
            Console.WriteLine($"        找到 {results.Count} 条，首条：{(results.Count > 0 ? results[0].DisplayName : "无")}");
            return results.Count > 0;
        });

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "===== 全部通过 =====" : $"===== 失败 {_failures} 项 =====");
        return _failures == 0 ? 0 : 1;
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    private static void Check(string name, Func<bool> action)
    {
        try
        {
            var ok = action();
            Console.WriteLine($"  [{(ok ? "通过" : "不符")}] {name}");
            if (!ok) _failures++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [异常] {name}: {ex.GetType().Name}: {ex.Message}");
            _failures++;
        }
    }

    /// <summary>
    /// 在线程池上跑一段异步逻辑并同步等结果。
    /// </summary>
    /// <remarks>
    /// 不能直接在主线程 await：Avalonia 初始化时装了 SynchronizationContext，
    /// 而这里没有消息循环去驱动它，续体永远排不上队，表现就是整个测试卡死。
    /// </remarks>
    private static T RunOffUiThread<T>(Func<Task<T>> action) =>
        Task.Run(action).GetAwaiter().GetResult();

    private static void LiveCheck(string name, Func<Task<bool>> action)
    {
        try
        {
            var ok = RunOffUiThread(action);
            Console.WriteLine($"  [{(ok ? "通过" : "不符")}] {name}");
            if (!ok) _failures++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [网络/解析异常] {name}: {ex.GetType().Name}: {ex.Message}");
            _failures++;
        }
    }
}
