using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ClassIsland.Promethium.Services;
using ClassIsland.Promethium.Components;
using ClassIsland.Promethium.Views.ComponentSettings;
using ClassIsland.Promethium.Views.SettingsPages;

internal static class Program
{
    private static int Main()
    {
        AppBuilder.Configure<Application>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = true })
            .SetupWithoutStarting();

        var folder = Path.Combine(Path.GetTempPath(), "pm-xamlcheck");
        Directory.CreateDirectory(folder);
        var store = new PromethiumConfigStore(folder);
        var geo = new NominatimService();
        var weather = new OpenMeteoService();

        var failures = 0;

        failures += Check("设置页 BetterWeatherSettingsPage", () =>
        {
            var page = new BetterWeatherSettingsPage(store, geo);
            return page.Content != null || page.GetVisualChildren() != null;
        });

        failures += Check("组件设置控件 BetterWeatherComponentSettingsControl", () =>
        {
            var control = new BetterWeatherComponentSettingsControl();
            return control.Content != null;
        });

        failures += Check("主界面组件 BetterWeatherComponent", () =>
        {
            var component = new BetterWeatherComponent(weather, store);
            return component.Content != null;
        });

        Console.WriteLine(failures == 0 ? "\n全部通过" : $"\n失败 {failures} 项");
        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, Func<bool> action)
    {
        try
        {
            var ok = action();
            Console.WriteLine($"  [{(ok ? "OK" : "空内容")}] {name}");
            return ok ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [异常] {name}: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return 1;
        }
    }
}
