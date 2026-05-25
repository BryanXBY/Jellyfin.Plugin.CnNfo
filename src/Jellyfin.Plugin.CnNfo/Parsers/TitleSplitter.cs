using System;
using System.Linq;

namespace Jellyfin.Plugin.CnNfo.Parsers;

/// <summary>
/// 把豆瓣 "原名 译名" 形式的标题拆成 中文标题 + 原名。
/// 豆瓣的约定是 中文在前、原名在后，但中间分隔符不一定是单空格。
/// </summary>
public class TitleSplitter
{
    public readonly record struct Result(string Chinese, string? Original);

    public Result Split(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new Result(string.Empty, null);
        }

        var title = raw.Trim();

        // 优先按空格切片
        var parts = title.Split(new[] { ' ', '\t', '　' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            // 从左到右扫描，找第一个 "明显是外文 / 日文" 的 segment
            for (var i = 1; i < parts.Length; i++)
            {
                if (LooksLikeOriginal(parts[i]))
                {
                    var chinese = string.Join(' ', parts[..i]).Trim();
                    var original = string.Join(' ', parts[i..]).Trim();
                    return new Result(chinese, NormalizeBlank(original));
                }
            }
        }

        // 找不到空格分界，则用 "最后一个 CJK 汉字" 做边界
        var lastCjk = -1;
        for (var i = 0; i < title.Length; i++)
        {
            if (IsChineseHan(title[i]))
            {
                lastCjk = i;
            }
        }
        if (lastCjk < 0)
        {
            // 整个标题没有汉字，那就当作原名
            return new Result(string.Empty, title);
        }
        if (lastCjk == title.Length - 1)
        {
            return new Result(title, null);
        }
        var idx = lastCjk + 1;
        while (idx < title.Length && (char.IsWhiteSpace(title[idx]) || char.IsPunctuation(title[idx])))
        {
            idx++;
        }
        if (idx >= title.Length)
        {
            return new Result(title, null);
        }
        var ch = title[..(lastCjk + 1)].Trim();
        var or = title[idx..].Trim();
        return new Result(ch, NormalizeBlank(or));
    }

    /// <summary>
    /// 当 Split 拿不到 original，但 ItemLookupInfo / 豆瓣别名里能提供候选时，
    /// 优先把 "不包含汉字、含拉丁/假名" 的别名当作 original。
    /// </summary>
    public string? PickOriginalFromAliases(System.Collections.Generic.IEnumerable<string> aliases)
    {
        return aliases
            .Select(a => a?.Trim() ?? string.Empty)
            .Where(a => a.Length > 0 && LooksLikeOriginal(a))
            .OrderByDescending(a => a.Length)
            .FirstOrDefault();
    }

    private static bool LooksLikeOriginal(string segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return false;
        }

        // 含日文假名 -> 必然是原名
        if (segment.Any(IsJapaneseKana))
        {
            return true;
        }

        // 全是拉丁字母 / 数字 / 标点 -> 是英文原名
        if (segment.All(c => IsLatinOrCommon(c)))
        {
            return true;
        }

        // 含韩文 -> 韩剧原名
        if (segment.Any(IsKorean))
        {
            return true;
        }

        return false;
    }

    private static bool IsChineseHan(char c)
        => (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);

    private static bool IsJapaneseKana(char c)
        => (c >= 0x3040 && c <= 0x309F) || (c >= 0x30A0 && c <= 0x30FF);

    private static bool IsKorean(char c)
        => c >= 0xAC00 && c <= 0xD7AF;

    private static bool IsLatinOrCommon(char c)
    {
        if (c < 0x80)
        {
            return true; // ASCII 全放行
        }
        // 拉丁扩展（含变音符号）
        if (c >= 0x00C0 && c <= 0x024F)
        {
            return true;
        }
        return false;
    }

    private static string NormalizeBlank(string s) => string.IsNullOrWhiteSpace(s) ? string.Empty : s;
}
