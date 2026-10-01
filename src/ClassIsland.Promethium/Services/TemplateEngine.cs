// Pm钷 v1.0.0.0 —— ClassIsland 综合增强插件
using System.Text;

namespace ClassIsland.Promethium.Services;

/// <summary>
/// 把用户文案里的 {变量} 换成真实值。
/// </summary>
/// <remarks>
/// 认不出来的变量原样留着，不做成空串——这样用户写错名字时能一眼看出来，
/// 而不是拿到一条莫名其妙少了半句话的提醒。
/// </remarks>
public static class TemplateEngine
{
    /// <summary>按给定的变量表渲染模板。</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var result = new StringBuilder(template.Length + 32);
        var index = 0;
        while (index < template.Length)
        {
            var open = template.IndexOf('{', index);
            if (open < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                result.Append(template, index, template.Length - index);
                break;
            }

            result.Append(template, index, open - index);
            var name = template.Substring(open + 1, close - open - 1).Trim();
            result.Append(variables.TryGetValue(name, out var value) ? value : "{" + name + "}");
            index = close + 1;
        }

        return result.ToString();
    }

    /// <summary>把变量名连成一句提示，放在设置页里给用户看。</summary>
    public static string Describe(IEnumerable<string> names) =>
        string.Join("、", names.Select(n => "{" + n + "}"));
}
