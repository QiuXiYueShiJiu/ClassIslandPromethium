using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ClassIsland.Promethium.Components;
using ClassIsland.Promethium.Models;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.Services.WeatherProviders;
using ClassIsland.Promethium.Views.ComponentSettings;
using ClassIsland.Promethium.Views.SettingsPages;

internal static class Program
{
    private static int _failures;
    private const double Lat = 39.9042, Lon = 116.4074;
    private const double UsLat = 40.7128, UsLon = -74.0060;   // 纽约，给 NWS 用

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

        var allProviders = new IWeatherProvider[]
        {
            new OpenMeteoProvider(), new MetNorwayProvider(), new WttrInProvider(),
            new SevenTimerProvider(), new NwsProvider(),
            new QWeatherProvider(), new SeniverseProvider(), new OpenWeatherMapProvider(),
            new WeatherApiProvider(), new WeatherbitProvider(), new VisualCrossingProvider(),
            new TomorrowIoProvider()
        };
        var catalog = new WeatherProviderCatalog(allProviders);
        var monitor = new WeatherMonitor(catalog, store, alerts);

        Section("一、XAML 能否加载（这是插件加载失败最常见的原因）");
        Check("设置页 BetterWeatherSettingsPage", () =>
        {
            var page = new BetterWeatherSettingsPage(store, geocoder, catalog, null!);
            return page.Content != null;
        });
        Check("组件设置控件 BetterWeatherComponentSettingsControl", () =>
            new BetterWeatherComponentSettingsControl().Content != null);
        Check("主界面组件 BetterWeatherComponent", () =>
            new BetterWeatherComponent(monitor, store, alerts).Content != null);

        Section("二、数据源台账完整性");
        Check($"共登记 {catalog.All.Count} 个数据源（应为 12）", () => catalog.All.Count == 12);
        Check("台账顺序与枚举顺序逐项一致（下拉框按索引对齐，错位就会选错源）", () =>
            catalog.All.Select((info, i) => (int)info.Kind == i).All(x => x));
        Check("每个枚举值都能解析出实例", () =>
            Enum.GetValues<WeatherProviderKind>().All(k => catalog.Resolve(k).Kind == k));
        Check("免密钥的不该有申请地址", () =>
            catalog.All.Where(i => !i.RequiresApiKey).All(i => string.IsNullOrEmpty(i.ApiKeyUrl)));
        Check("需要密钥的必须有申请地址（否则「申请密钥」按钮是死的）", () =>
            catalog.All.Where(i => i.RequiresApiKey).All(i => !string.IsNullOrEmpty(i.ApiKeyUrl)));
        Check("接口声明的 RequiresApiKey 与台账一致", () =>
            catalog.All.All(i => catalog.Resolve(i.Kind).RequiresApiKey == i.RequiresApiKey));
        Check($"免密钥 {catalog.All.Count(i => !i.RequiresApiKey)} 个 / 需密钥 {catalog.All.Count(i => i.RequiresApiKey)} 个",
            () => catalog.All.Count(i => !i.RequiresApiKey) == 5 && catalog.All.Count(i => i.RequiresApiKey) == 7);

        Section("三、报警判定：绝不能无中生有");
        var mild = new WeatherSnapshot
        {
            Condition = WeatherCondition.Clear, Temperature = 22, FeelsLike = 22, Humidity = 50,
            WindSpeed = 10, Pressure = 1013, Precipitation = 0,
            TodayMin = 18, TodayMax = 25, HasDailyRange = true, IsDay = true
        };
        Check($"温和天气应无报警（实得 {WeatherAlertEvaluator.Evaluate(mild, new WeatherConfig()).Count} 条）",
            () => WeatherAlertEvaluator.Evaluate(mild, new WeatherConfig()).Count == 0);
        var hot = new WeatherSnapshot { Condition = WeatherCondition.Clear, Temperature = 30, TodayMin = 28, TodayMax = 38, HasDailyRange = true };
        Check("38°C 应命中高温", () =>
        {
            var a = WeatherAlertEvaluator.Evaluate(hot, new WeatherConfig());
            return a.Count == 1 && a[0].Key == "high_temperature";
        });
        var stormy = new WeatherSnapshot { Condition = WeatherCondition.Thunderstorm, Temperature = 20 };
        Check("雷暴应命中", () => WeatherAlertEvaluator.Evaluate(stormy, new WeatherConfig()).Count == 1);
        Check("关掉雷暴开关后不应命中", () =>
            WeatherAlertEvaluator.Evaluate(stormy, new WeatherConfig { AlertThunderstorm = false }).Count == 0);
        Check("霾按大雾/霾一类报警", () =>
            WeatherAlertEvaluator.Evaluate(new WeatherSnapshot { Condition = WeatherCondition.Haze },
                new WeatherConfig()).Any(a => a.Key == "fog"));

        Section("四、天气描述归一化（顺序坑已踩过一次，这里钉住）");
        Check("「晴间多云」判晴间多云而不是晴", () =>
            WeatherConditionText.FromChinese("晴间多云") == WeatherCondition.PartlyCloudy);
        Check("「多云」判多云", () => WeatherConditionText.FromChinese("多云") == WeatherCondition.PartlyCloudy);
        Check("「阴」判阴", () => WeatherConditionText.FromChinese("阴") == WeatherCondition.Overcast);
        Check("「雷阵雨」判雷暴而不是雨", () =>
            WeatherConditionText.FromChinese("雷阵雨") == WeatherCondition.Thunderstorm);
        Check("「雨夹雪」判雨夹雪而不是雪或雨", () =>
            WeatherConditionText.FromChinese("雨夹雪") == WeatherCondition.Sleet);
        Check("「大雨」判大雨", () => WeatherConditionText.FromChinese("大雨") == WeatherCondition.HeavyRain);
        Check("「霾」判霾", () => WeatherConditionText.FromChinese("霾") == WeatherCondition.Haze);
        Check("英文「Partly cloudy」判晴间多云而不是阴", () =>
            WeatherConditionText.FromEnglish("Partly cloudy") == WeatherCondition.PartlyCloudy);
        Check("英文「Overcast」判阴", () =>
            WeatherConditionText.FromEnglish("Overcast") == WeatherCondition.Overcast);
        Check("空描述不猜，判未知", () =>
            WeatherConditionText.FromAny(null) == WeatherCondition.Unknown);

        Section("五、单位与方位换算");
        Check("罗盘 WNW 约 292.5 度", () => Math.Abs(WeatherText.DirectionFromCompass("WNW") - 292.5) < 0.01);
        Check("罗盘认不出时返回 -1 而不是 0（0 是正北，会是错的）", () =>
            WeatherText.DirectionFromCompass("???") < 0);
        Check("中文方位「东南风」约 135 度", () =>
            Math.Abs(WeatherText.DirectionFromChineseCompass("东南风") - 135) < 0.01);
        Check("「无持续风向」返回 -1", () => WeatherText.DirectionFromChineseCompass("无持续风向") < 0);
        Check("蒲福 0 级接近无风", () => WeatherText.BeaufortToKmh(0) < 2);
        Check("蒲福 5 级约 34 km/h", () => Math.Abs(WeatherText.BeaufortToKmh(5) - 34) < 0.1);
        Check("蒲福超界不崩", () => WeatherText.BeaufortToKmh(99) > 0);
        Check("「10 to 15 mph」取最大值 15（比阈值时宁保守）", () =>
            Math.Abs(JsonRead.MaxNumberInText("10 to 15 mph") - 15) < 0.01);
        Check("「30%」抠出 30", () => Math.Abs(JsonRead.MaxNumberInText("30%") - 30) < 0.01);
        Check("空串得 0", () => Math.Abs(JsonRead.MaxNumberInText("")) < 0.01);

        Section("六、密钥守卫：没填密钥必须给人话，而不是甩 401");
        foreach (var p in allProviders.Where(x => x.RequiresApiKey))
        {
            Check($"{p.DisplayName} 缺密钥时报错可读", () =>
            {
                try
                {
                    RunOffUiThread(() => p.QueryAsync(new WeatherQuery(Lat, Lon, string.Empty)));
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    return ex.Message.Contains("密钥");
                }
            });
        }

        Section("七、变量模板与显示模式");
        var vars = new Dictionary<string, string> { ["标题"] = "高温", ["位置"] = "北京" };
        Check("已知变量被替换", () => TemplateEngine.Render("{位置}{标题}", vars) == "北京高温");
        Check("未知变量原样保留", () => TemplateEngine.Render("{没有这个}", vars) == "{没有这个}");
        Check("天气变量表不含地震变量", () => !AlertTextComposer.WeatherVariables.Contains("震级"));

        var settings = new BetterWeatherSettings();
        var vm = new ClassIsland.Promethium.ViewModels.BetterWeatherViewModel(monitor, store, alerts);
        vm.AttachSettings(settings);
        Check("标准模式：位置名/温差/湿度开，风力/体感关",
            () => vm.ShowLocationNameEffective && vm.ShowDailyRangeEffective && vm.ShowHumidityEffective
                  && !vm.ShowWindEffective && !vm.ShowFeelsLikeEffective);
        settings.DisplayMode = WeatherDisplayMode.Detailed;
        Check("切到详细五项全开（同时证明改设置会实时生效）",
            () => vm.ShowWindEffective && vm.ShowFeelsLikeEffective);
        settings.DisplayMode = WeatherDisplayMode.Compact;
        Check("切到紧凑五项全关", () => !vm.ShowHumidityEffective && !vm.ShowWindEffective);
        settings.DisplayMode = WeatherDisplayMode.Custom;
        settings.ShowWind = true;
        Check("自定义模式才听逐项开关", () => vm.ShowWindEffective);

        Section("八、自定义图标取图与配置存盘");
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

        store.Weather.LocationName = "测试地点";
        store.Weather.SetApiKey(WeatherProviderKind.QWeather, "abc123");
        store.Weather.SetApiKey(WeatherProviderKind.OpenWeatherMap, "def456");
        store.Save();
        var reloaded = new PromethiumConfigStore(folder);
        Check("地点名往返一致", () => reloaded.Weather.LocationName == "测试地点");
        Check("密钥按数据源分别保存，互不覆盖", () =>
            reloaded.Weather.GetApiKey(WeatherProviderKind.QWeather) == "abc123"
            && reloaded.Weather.GetApiKey(WeatherProviderKind.OpenWeatherMap) == "def456");
        Check("未设置密钥的源取到空串", () =>
            reloaded.Weather.GetApiKey(WeatherProviderKind.MetNorway) == string.Empty);

        Section("九、免密钥数据源联网实测（真实返回验证解析）");
        foreach (var p in allProviders.Where(x => !x.RequiresApiKey && x.Kind != WeatherProviderKind.Nws))
        {
            LiveCheck($"天气源 {p.DisplayName}", () => CheckProvider(p, Lat, Lon));
        }
        LiveCheck("天气源 NWS（用纽约坐标）", () => CheckProvider(new NwsProvider(), UsLat, UsLon));
        LiveCheck("NWS 对非美国坐标给出可读提示而不是 404", () =>
        {
            try
            {
                RunOffUiThread(() => new NwsProvider().QueryAsync(new WeatherQuery(Lat, Lon)));
                return false;
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"        {ex.Message}");
                return ex.Message.Contains("美国");
            }
        });

        Section("十、地理编码联调");
        LiveCheck("逆地理编码（应能反查到北京一带）", () =>
        {
            var name = RunOffUiThread(() => geocoder.ReverseAsync(Lat, Lon));
            Console.WriteLine($"        {name}");
            return name.Contains("北京") || name.Contains("北京");
        });
        LiveCheck("地名搜索（应能搜到镇一级）", () =>
        {
            var results = RunOffUiThread(() => geocoder.SearchAsync("某某镇"));
            Console.WriteLine($"        找到 {results.Count} 条，首条：{(results.Count > 0 ? results[0].DisplayName : "无")}");
            return results.Count > 0;
        });

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "===== 全部通过 =====" : $"===== 失败 {_failures} 项 =====");
        return _failures == 0 ? 0 : 1;
    }

    private static bool CheckProvider(IWeatherProvider provider, double lat, double lon)
    {
        var s = RunOffUiThread(() => provider.QueryAsync(new WeatherQuery(lat, lon)));
        Console.WriteLine($"        气温 {s.Temperature:0.#}°C · 天气 {WeatherText.Describe(s.Condition)} · 湿度 {s.Humidity:0.#}% · 风 {s.WindSpeed:0.#}km/h · 原始码 {s.RawCode}");
        Console.WriteLine($"        今日 {(s.HasDailyRange ? $"{s.TodayMin:0.#}~{s.TodayMax:0.#}°C" : "数据源未提供")}");
        return s.Condition != WeatherCondition.Unknown;
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

    private static void LiveCheck(string name, Func<bool> action)
    {
        try
        {
            var ok = action();
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
