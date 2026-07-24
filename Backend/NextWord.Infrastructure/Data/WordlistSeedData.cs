using System.Text.Json;
using System.Text.Json.Serialization;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Scenarios;

namespace NextWord.Infrastructure.Data;

/// <summary>
/// 内置精选词表（T-002）：由 Backend/Scripts/generate-wordlist.py 按设计方案 §4 标准生成，
/// 每词已带 scenarios（0–3 个子场景，0 个 = core 通用桶）、utility（low 已在生成期过滤）、role 标注，
/// 因此入库即视为已标注（ScenarioAnnotationVersion = 当前版本），不依赖运行时 LLM。
/// </summary>
public static class WordlistSeedData
{
    private const string ResourceName = "NextWord.Infrastructure.Data.wordlist-scenarios.json";

    public static IReadOnlyList<WordlistEntry> LoadEntries()
    {
        var assembly = typeof(WordlistSeedData).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {ResourceName}");
        var document = JsonSerializer.Deserialize<WordlistDocument>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Wordlist resource is empty.");
        return document.Words;
    }

    public static Word ToWord(WordlistEntry entry)
    {
        var cefr = Enum.TryParse<CefrLevel>(entry.Cefr, ignoreCase: true, out var parsed) ? parsed : CefrLevel.A2;
        var word = new Word
        {
            Lemma = entry.Lemma,
            PartOfSpeech = entry.Pos,
            Phonetics = entry.Phonetics,
            Meanings = entry.Meanings,
            ExampleSentences = entry.Examples,
            CefrLevel = cefr,
            DifficultyLevel = cefr switch
            {
                CefrLevel.A1 or CefrLevel.A2 => DifficultyLevel.Basic,
                CefrLevel.B1 or CefrLevel.B2 => DifficultyLevel.Intermediate,
                _ => DifficultyLevel.Advanced
            },
            IsCore = true,
            Utility = Enum.TryParse<WordUtility>(entry.Utility, ignoreCase: true, out var utility) ? utility : WordUtility.Medium,
            Role = Enum.TryParse<ExpressionRole>(entry.Role, ignoreCase: true, out var role) ? role : ExpressionRole.SceneNoun,
            ScenarioAnnotationVersion = Services.ScenarioAnnotationWorker.CurrentVersion
        };

        foreach (var key in entry.Scenarios.Where(ScenarioTaxonomy.IsSubScenarioKey).Take(3))
        {
            word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = key });
        }

        return word;
    }

    public sealed class WordlistDocument
    {
        [JsonPropertyName("words")]
        public List<WordlistEntry> Words { get; set; } = [];
    }

    public sealed class WordlistEntry
    {
        [JsonPropertyName("lemma")]
        public string Lemma { get; set; } = string.Empty;

        [JsonPropertyName("pos")]
        public string Pos { get; set; } = string.Empty;

        [JsonPropertyName("phonetics")]
        public string Phonetics { get; set; } = string.Empty;

        [JsonPropertyName("meanings")]
        public List<string> Meanings { get; set; } = [];

        [JsonPropertyName("examples")]
        public List<string> Examples { get; set; } = [];

        [JsonPropertyName("cefr")]
        public string Cefr { get; set; } = "A2";

        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("utility")]
        public string Utility { get; set; } = string.Empty;

        [JsonPropertyName("scenarios")]
        public List<string> Scenarios { get; set; } = [];
    }
}
