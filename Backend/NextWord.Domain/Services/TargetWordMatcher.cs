namespace NextWord.Domain.Services;

/// <summary>
/// T-040 统一命中口径（纯函数无依赖）：目标词（单词或多词短语）在文本中是否出现。
/// 单词走词边界匹配（不误伤子串）；多词短语按词序列连续匹配——大小写不敏感、
/// 容忍标点/多余空白分隔（"up, in arms,"、"up  in  arms" 均命中），词序必须一致
/// （"armed up in" 不命中）；不做词形变换，原样小写词序列匹配。
/// 所有生命周期命中判定（造句确认、使用错误回退、自发毕业）统一走这里，
/// 不再各自分词副本（瓶颈筛查的安全词内容词口径另管，见 BottleneckScreeningService）。
/// </summary>
public static class TargetWordMatcher
{
    /// <summary>目标词在文本中是否命中（单词词边界 / 短语连续词序列）。</summary>
    public static bool IsHit(string target, string? text)
    {
        var targetTokens = Tokenize(target);
        if (targetTokens.Count == 0 || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var textTokens = Tokenize(text);
        if (targetTokens.Count == 1)
        {
            var token = targetTokens[0];
            return textTokens.Any(textToken => textToken == token);
        }

        // 多词短语：连续子序列匹配
        for (var start = 0; start + targetTokens.Count <= textTokens.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < targetTokens.Count; offset++)
            {
                if (textTokens[start + offset] != targetTokens[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>分词：连续小写字母序列为一个词（标点/空白/数字都是分隔符，与既有词边界口径一致）。</summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetter(ch))
            {
                current.Append(char.ToLowerInvariant(ch));
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
