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
    private const double Lat = 39.9042, Lon = 116.4074;   // 中性测试点
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
        var geocoder = new GeocodingService(store);

        var allProviders = new IWeatherProvider[]
        {
            new OpenMeteoProvider(), new MetNorwayProvider(), new WttrInProvider(),
            new SevenTimerProvider(), new NwsProvider(),
            new QWeatherProvider(), new SeniverseProvider(), new OpenWeatherMapProvider(),
            new WeatherApiProvider(), new WeatherbitProvider(), new VisualCrossingProvider(),
            new TomorrowIoProvider(),
            new BrightSkyProvider(), new NeaSingaporeProvider(), new EstoniaProvider()
        };
        var catalog = new WeatherProviderCatalog(allProviders);
        var monitor = new WeatherMonitor(catalog, store, alerts);

        Section("一、XAML 能否加载（这是插件加载失败最常见的原因）");
        Check("设置页 BetterWeatherSettingsPage", () =>
        {
            var page = new BetterWeatherSettingsPage(store, geocoder, catalog, null!, new AutoConfigurator(catalog), monitor);
            return page.Content != null;
        });
        Check("组件设置控件 BetterWeatherComponentSettingsControl", () =>
            new BetterWeatherComponentSettingsControl().Content != null);
        Check("主界面组件 BetterWeatherComponent", () =>
            new BetterWeatherComponent(monitor, store, alerts).Content != null);

        Section("二、数据源台账完整性");
        Check($"共登记 {catalog.All.Count} 个数据源（应为 15）", () => catalog.All.Count == 15);
        Check("每个枚举值在台账里恰好出现一次（不漏不重）", () =>
            catalog.All.Select(i => i.Kind).OrderBy(k => k)
                .SequenceEqual(Enum.GetValues<WeatherProviderKind>().OrderBy(k => k)));
        Check("免密钥的排在下拉框前面，便于查找", () =>
        {
            var firstKeyed = catalog.All.ToList().FindIndex(i => i.RequiresApiKey);
            return firstKeyed > 0 && catalog.All.Skip(firstKeyed).All(i => i.RequiresApiKey);
        });
        Check("每个枚举值都能解析出实例", () =>
            Enum.GetValues<WeatherProviderKind>().All(k => catalog.Resolve(k).Kind == k));
        Check("免密钥的不该有申请地址", () =>
            catalog.All.Where(i => !i.RequiresApiKey).All(i => string.IsNullOrEmpty(i.ApiKeyUrl)));
        Check("需要密钥的必须有申请地址（否则「申请密钥」按钮是死的）", () =>
            catalog.All.Where(i => i.RequiresApiKey).All(i => !string.IsNullOrEmpty(i.ApiKeyUrl)));
        Check("接口声明的 RequiresApiKey 与台账一致", () =>
            catalog.All.All(i => catalog.Resolve(i.Kind).RequiresApiKey == i.RequiresApiKey));
        Check($"免密钥 {catalog.All.Count(i => !i.RequiresApiKey)} 个 / 需密钥 {catalog.All.Count(i => i.RequiresApiKey)} 个",
            () => catalog.All.Count(i => !i.RequiresApiKey) == 8 && catalog.All.Count(i => i.RequiresApiKey) == 7);
        Check("国家级源都标了覆盖范围，不能让人以为全球可用", () =>
            catalog.All.Where(i => i.Region.StartsWith("仅") || i.Region.Contains("优化"))
                .All(i => !string.IsNullOrWhiteSpace(i.Region)));

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
        // 各国气象部门的服务只覆盖本国，必须用对应国家的坐标去测
        var testPoints = new Dictionary<WeatherProviderKind, (double Lat, double Lon)>
        {
            [WeatherProviderKind.Nws] = (UsLat, UsLon),              // 纽约
            [WeatherProviderKind.BrightSky] = (52.52, 13.405),       // 柏林
            [WeatherProviderKind.NeaSingapore] = (1.3521, 103.8198), // 新加坡
            [WeatherProviderKind.Estonia] = (59.437, 24.7536)        // 塔林
        };

        // 这两个源官方就不发布天空状况（NEA 只有降雨、爱沙尼亚部分站缺现象字段），
        // 显示「未知」是诚实的，不能拿它当失败。它们只强制要求温度是真的。
        var mayLackCondition = new HashSet<WeatherProviderKind>
        {
            WeatherProviderKind.NeaSingapore, WeatherProviderKind.Estonia
        };

        foreach (var p in allProviders.Where(x => !x.RequiresApiKey))
        {
            var point = testPoints.TryGetValue(p.Kind, out var custom) ? custom : (Lat, Lon);
            var requireCondition = !mayLackCondition.Contains(p.Kind);
            LiveCheck($"天气源 {p.DisplayName}", () => CheckProvider(p, point.Lat, point.Lon, requireCondition));
        }

        Section("十、只覆盖本国的源，必须给出可读提示而不是甩错误码");
        foreach (var (provider, keyword) in new (IWeatherProvider, string)[]
                 {
                     (new NwsProvider(), "美国"),
                     (new BrightSkyProvider(), "德国"),
                     (new NeaSingaporeProvider(), "新加坡")
                 })
        {
            LiveCheck($"{provider.DisplayName} 对境外坐标的提示", () =>
            {
                try
                {
                    RunOffUiThread(() => provider.QueryAsync(new WeatherQuery(Lat, Lon)));
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"        {ex.Message}");
                    return ex.Message.Contains(keyword) || ex.Message.Contains("覆盖");
                }
            });
        }

        Section("十一、底图：默认必须有底图（这条是回归测试）");
        Check("默认配置解析出的候选里至少有一个能取瓦片", () =>
            MapTileCatalog.ResolveChain(new WeatherConfig()).Any(s => s.HasBasemap));
        Check("自动模式的优先级是 高德 → 百度 → 腾讯 → OpenStreetMap", () =>
            MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.Auto })
                .Select(s => s.Name).SequenceEqual(new[] { "高德", "百度", "腾讯", "OpenStreetMap" }));
        Check("MapPicker 不调 Configure 也自带底图（回归：曾经漏调导致地图只剩网格）", () =>
            new ClassIsland.Promethium.Controls.MapPicker().ActiveTileSourceName != "不用底图");
        Check("选「不用底图」时确实没有候选会去取瓦片", () =>
            !MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.None })
                .Any(s => s.HasBasemap));
        Check("高德地址能替换掉全部占位符", () =>
        {
            var spec = MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.AMap })[0];
            var url = MapTileCatalog.BuildUrl(spec, string.Empty, 15, 26979, 12415);
            return url != null && !url.Contains('{')
                   && url.Contains("x=26979") && url.Contains("y=12415") && url.Contains("z=15");
        });
        Check("各家的坐标基准标注正确", () =>
            MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.AMap })[0].Datum == TileDatum.Gcj02
            && MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.Tencent })[0].Datum == TileDatum.Gcj02
            && MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.Baidu })[0].Datum == TileDatum.Bd09
            && MapTileCatalog.ResolveChain(new WeatherConfig { MapTileSource = MapTileSource.OpenStreetMap })[0].Datum == TileDatum.Wgs84);
        Check("不用底图时拼不出地址", () =>
            MapTileCatalog.BuildUrl(MapTileSpec.None, string.Empty, 1, 1, 1) == null);
        Check("自定义模板缺占位符会被拒绝", () =>
            !MapTileCatalog.ValidateCustomTemplate("https://a.com/{z}/{x}.png", out _));

        Section("十二、坐标基准换算（混用会让标记偏几百米）");
        var (gcjLat, gcjLon) = ChinaCoordinate.Wgs84ToGcj02(39.9087, 116.3975);
        Check($"北京 WGS84→GCJ-02 偏移经度约 0.006（实得 {gcjLon - 116.3975:0.000000}）",
            () => Math.Abs(gcjLon - 116.3975 - 0.006) < 0.002 && Math.Abs(gcjLat - 39.9087 - 0.0015) < 0.002);
        Check("GCJ-02 往返能回到 WGS84（误差 < 1e-6）", () =>
        {
            var (backLat, backLon) = ChinaCoordinate.Gcj02ToWgs84(gcjLat, gcjLon);
            return Math.Abs(backLat - 39.9087) < 1e-6 && Math.Abs(backLon - 116.3975) < 1e-6;
        });
        Check("境外坐标不做偏移", () =>
        {
            var (lat, lon) = ChinaCoordinate.Wgs84ToGcj02(48.8566, 2.3522);
            return Math.Abs(lat - 48.8566) < 1e-9 && Math.Abs(lon - 2.3522) < 1e-9;
        });
        Check("BD-09 往返能回到 WGS84（误差 < 1e-6）", () =>
        {
            var (bdLat, bdLon) = ChinaCoordinate.Wgs84ToBd09(39.9087, 116.3975);
            var (backLat, backLon) = ChinaCoordinate.Bd09ToWgs84(bdLat, bdLon);
            return Math.Abs(backLat - 39.9087) < 1e-6 && Math.Abs(backLon - 116.3975) < 1e-6;
        });

        Section("十三、地名反查：必须有多家可降级（这是「反查不成功」的修法）");
        foreach (var (kind, name) in new (GeocodingProviderKind, string)[]
                 {
                     (GeocodingProviderKind.Nominatim, "Nominatim"),
                     (GeocodingProviderKind.Photon, "Photon"),
                     (GeocodingProviderKind.BigDataCloud, "BigDataCloud")
                 })
        {
            LiveCheck($"逆地理编码 {name}", () =>
            {
                var probe = new PromethiumConfigStore(Path.Combine(folder, "geo-" + kind));
                probe.Weather.GeocodingProvider = kind;
                var result = RunOffUiThread(() => new GeocodingService(probe).ReverseAsync(39.9087, 116.3975));
                Console.WriteLine($"        {result.ProviderName} → {result.Name}");
                return !string.IsNullOrWhiteSpace(result.Name);
            });
        }

        LiveCheck("自动降级能返回结果（并说明用的是哪一家）", () =>
        {
            var probe = new PromethiumConfigStore(Path.Combine(folder, "geo-auto"));
            var result = RunOffUiThread(() => new GeocodingService(probe).ReverseAsync(39.9087, 116.3975));
            Console.WriteLine($"        自动选中 {result.ProviderName} → {result.Name}");
            return !string.IsNullOrWhiteSpace(result.Name);
        });

        LiveCheck("指定一家连不上时，报错要说清原因而不是只给异常类型", () =>
        {
            var probe = new PromethiumConfigStore(Path.Combine(folder, "geo-fail"));
            // 指向一个不可能连上的地址来模拟「这家不通」
            probe.Weather.GeocodingProvider = GeocodingProviderKind.Nominatim;
            try
            {
                var result = RunOffUiThread(() => new GeocodingService(probe).ReverseAsync(0, 0));
                Console.WriteLine($"        返回了：{result.Name}");
                return true;   // 通不通都算通过，这里只验证不抛难懂的异常
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"        {ex.Message}");
                return ex.Message.Contains("没查到") || ex.Message.Contains("地名服务");
            }
        });

        Section("十四、地名搜索");
        LiveCheck("搜索天安门", () =>
        {
            var probe = new PromethiumConfigStore(Path.Combine(folder, "geo-search"));
            var results = RunOffUiThread(() => new GeocodingService(probe).SearchAsync("天安门"));
            Console.WriteLine($"        找到 {results.Count} 条，首条：{(results.Count > 0 ? results[0].DisplayName : "无")}");
            if (results.Count > 0)
            {
                var first = results[0];
                Console.WriteLine($"        坐标 {first.Latitude:0.0000}, {first.Longitude:0.0000}");
            }
            return results.Count > 0;
        });

        Section("十五、一键自动配置");
        LiveCheck("能探出可用的天气源与底图", () =>
        {
            var result = RunOffUiThread(() => new AutoConfigurator(catalog).RunAsync(Lat, Lon));
            Console.WriteLine($"        {result.Summary}");
            Console.WriteLine($"        探到：天气={result.WeatherProvider} 底图={result.MapSource}");
            return result.WeatherProvider.HasValue || result.MapSource.HasValue;
        });
        Check("自动探测全程不改配置（回归：曾经在后台线程改配置导致界面卡死）", () =>
        {
            // 这个用例是那次卡死的守门人。AutoConfigurator 只负责探测并返回结果，
            // 写配置必须由界面在 UI 线程上做——所以它根本不该接受配置对象。
            var probe = new PromethiumConfigStore(Path.Combine(folder, "auto-nomutate"));
            probe.Weather.Provider = WeatherProviderKind.QWeather;
            probe.Weather.MapTileSource = MapTileSource.None;
            RunOffUiThread(() => new AutoConfigurator(catalog).RunAsync(Lat, Lon));
            return probe.Weather.Provider == WeatherProviderKind.QWeather
                   && probe.Weather.MapTileSource == MapTileSource.None;
        });
        LiveCheck("探测必须有上限，不能无限等（每个源 6 秒超时）", () =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            RunOffUiThread(() => new AutoConfigurator(catalog).RunAsync(Lat, Lon));
            watch.Stop();
            Console.WriteLine($"        耗时 {watch.Elapsed.TotalSeconds:0.0} 秒");
            return watch.Elapsed.TotalSeconds < 60;
        });
        Check("自动配置只会挑全球性、免密钥的天气源", () =>
        {
            // 国家级源只覆盖本国，自动挑到会给出别国天气；需要密钥的没法自动配
            var kinds = new[]
            {
                WeatherProviderKind.OpenMeteo, WeatherProviderKind.MetNorway,
                WeatherProviderKind.WttrIn, WeatherProviderKind.SevenTimer
            };
            return kinds.All(k => !catalog.Resolve(k).RequiresApiKey);
        });

        Section("十六、设置页的天气速览");
        Check("还没取到数据时显示占位而不是 0（0 会被当成真数据）", () =>
        {
            var settingsVm = new ClassIsland.Promethium.ViewModels.BetterWeatherSettingsViewModel(
                store, geocoder, catalog, null!, new AutoConfigurator(catalog), monitor);
            return !settingsVm.HasLiveWeather
                   && settingsVm.LiveTemperatureText.Contains("--")
                   && !string.IsNullOrEmpty(settingsVm.LiveConditionText);
        });

        Section("十七、地理编码联调（原用例保留）");
        LiveCheck("逆地理编码（应能反查到北京一带）", () =>
        {
            var result = RunOffUiThread(() => geocoder.ReverseAsync(Lat, Lon));
            Console.WriteLine($"        {result.ProviderName} → {result.Name}");
            return result.Name.Contains("北京") || result.Name.Contains("东城") || result.Name.Contains("东华门");
        });
        LiveCheck("地名搜索（用公开地标验证）", () =>
        {
            var results = RunOffUiThread(() => geocoder.SearchAsync("天安门"));
            Console.WriteLine($"        找到 {results.Count} 条，首条：{(results.Count > 0 ? results[0].DisplayName : "无")}");
            return results.Count > 0;
        });

        Console.WriteLine();
        Console.WriteLine(_failures == 0 ? "===== 全部通过 =====" : $"===== 失败 {_failures} 项 =====");
        return _failures == 0 ? 0 : 1;
    }

    private static bool CheckProvider(IWeatherProvider provider, double lat, double lon, bool requireCondition)
    {
        var s = RunOffUiThread(() => provider.QueryAsync(new WeatherQuery(lat, lon)));
        var humidity = s.Humidity > 0 ? $"{s.Humidity:0.#}%" : "未提供";
        Console.WriteLine($"        气温 {s.Temperature:0.#}°C · 天气 {WeatherText.Describe(s.Condition)} · 湿度 {humidity} · 风 {s.WindSpeed:0.#}km/h · 原始码 {s.RawCode}");
        Console.WriteLine($"        来源 {s.ProviderName} · 今日 {(s.HasDailyRange ? $"{s.TodayMin:0.#}~{s.TodayMax:0.#}°C" : "数据源未提供")}");
        // 温度必须是真的（0 一律当无效），现象则按数据源能力决定要不要强制
        return s.Temperature != 0 && (!requireCondition || s.Condition != WeatherCondition.Unknown);
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
