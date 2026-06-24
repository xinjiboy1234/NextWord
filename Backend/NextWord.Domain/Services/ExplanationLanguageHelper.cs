namespace NextWord.Domain.Services;

public static class ExplanationLanguageHelper
{
    public const string Default = "zh-CN";

    private static readonly Dictionary<string, string> PromptDisplayNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["zh-CN"] = "Chinese (Simplified)",
            ["zh-TW"] = "Chinese (Traditional)",
            ["zh-HK"] = "Chinese (Traditional)",
            ["en"] = "English",
            ["en-US"] = "English",
            ["en-GB"] = "English",
            ["ja"] = "Japanese",
            ["ja-JP"] = "Japanese",
            ["ko"] = "Korean",
            ["ko-KR"] = "Korean",
            ["es"] = "Spanish",
            ["es-ES"] = "Spanish",
            ["fr"] = "French",
            ["fr-FR"] = "French",
            ["de"] = "German",
            ["de-DE"] = "German",
        };

    public static string Resolve(string? requestLanguage, string configuredDefault)
    {
        if (!string.IsNullOrWhiteSpace(requestLanguage))
        {
            return requestLanguage.Trim();
        }

        return string.IsNullOrWhiteSpace(configuredDefault) ? Default : configuredDefault.Trim();
    }

    public static string GetPromptDisplayName(string locale)
    {
        if (PromptDisplayNames.TryGetValue(locale, out var displayName))
        {
            return displayName;
        }

        var primary = locale.Split('-', '_')[0];
        if (PromptDisplayNames.TryGetValue(primary, out displayName))
        {
            return displayName;
        }

        return locale;
    }

    public static bool IsChinese(string locale)
        => locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
}
