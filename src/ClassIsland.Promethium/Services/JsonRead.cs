// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Globalization;
using System.Text.Json;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 读 JSON 的小工具，全部「读不到就给默认值」，不抛异常。
/// </summary>
/// <remarks>
/// 各家的返回结构差别很大，字段偶尔还会缺。要是每个字段都写一遍
/// TryGetProperty + ValueKind 判断，代码会淹在防御里；
/// 但完全不防御，一个字段缺失就会让整次取数废掉。折中成这几个helper。
/// <para/>
/// 数字字段尤其要小心：不少接口把数值写成字符串（<c>"30%"</c>、<c>"5 mph"</c>），
/// 所以这里对字符串也会尝试解析。
/// </remarks>
public static class JsonRead
{
    /// <summary>读数字，支持数值和数字字符串。</summary>
    public static double Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return 0d;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => ParseLeadingNumber(value.GetString()),
            _ => 0d
        };
    }

    /// <summary>读字符串，非字符串返回空串。</summary>
    public static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>读布尔，数字 1 也算真。</summary>
    public static bool Flag(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => value.GetDouble() > 0.5,
            JsonValueKind.String => value.GetString() is "1" or "true" or "True",
            _ => false
        };
    }

    /// <summary>按路径逐层深入读数字。</summary>
    public static double Nested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return 0d;
            }
        }

        return current.ValueKind switch
        {
            JsonValueKind.Number => current.GetDouble(),
            JsonValueKind.String => ParseLeadingNumber(current.GetString()),
            _ => 0d
        };
    }

    /// <summary>按路径逐层深入读字符串。</summary>
    public static string NestedText(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var name in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out current))
            {
                return string.Empty;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>
    /// 从字符串里抠出所有数字并返回最大值。
    /// </summary>
    /// <remarks>
    /// 有些接口把风速写成 <c>"10 to 15 mph"</c>。取最大值而不是第一个数，
    /// 是因为这个值要拿去比报警阈值，宁可偏保守。
    /// </remarks>
    public static double MaxNumberInText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        var max = 0d;
        var found = false;
        var index = 0;
        while (index < text.Length)
        {
            if (char.IsDigit(text[index]) || (text[index] == '-' && index + 1 < text.Length && char.IsDigit(text[index + 1])))
            {
                var start = index;
                if (text[index] == '-')
                {
                    index++;
                }

                while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.'))
                {
                    index++;
                }

                if (double.TryParse(text.AsSpan(start, index - start), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed))
                {
                    max = found ? Math.Max(max, parsed) : parsed;
                    found = true;
                }
            }
            else
            {
                index++;
            }
        }

        return max;
    }

    /// <summary>抠出字符串开头的第一个数字，读不到返回 0。</summary>
    public static double ParseLeadingNumber(string? text) => MaxNumberInText(text);
}
