// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClassIsland.Promethium.Models;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 把插件的全局配置存到宿主给的插件设置目录里。
/// </summary>
/// <remarks>
/// 写文件走「先写临时文件再替换」——中途断电或崩溃时，
/// 至少不会把原来那份好的配置截断成半截 JSON。
/// </remarks>
public class PromethiumConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 不转义中文，配置文件人也要能读
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _filePath;

    /// <summary>「更好的天气」的全局配置。</summary>
    public WeatherConfig Weather { get; private set; }

    public PromethiumConfigStore(string configFolder)
    {
        _filePath = Path.Combine(configFolder, "settings.json");
        var loaded = Load();
        Weather = loaded.Weather ?? new WeatherConfig();
    }

    private ConfigRoot Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<ConfigRoot>(json, Options);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // 配置坏了不该让插件起不来，用默认值继续，下一次保存会覆盖它
        }

        return new ConfigRoot();
    }

    /// <summary>把当前配置写回磁盘。</summary>
    public void Save()
    {
        try
        {
            var root = new ConfigRoot { Weather = Weather };
            var json = JsonSerializer.Serialize(root, Options);
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _filePath, true);
        }
        catch (Exception)
        {
            // 存不下就算了，不能因为这个把宿主拖崩
        }
    }

    /// <summary>配置文件的外层结构，方便以后往里加别的模块的配置。</summary>
    private class ConfigRoot
    {
        public WeatherConfig? Weather { get; set; }
    }

    /// <summary>任一配置被改动就落盘。</summary>
    public void WatchForChanges()
    {
        Weather.PropertyChanged += (_, _) => Save();
    }
}
