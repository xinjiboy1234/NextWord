using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

internal sealed class DefinitionJson
{
    [JsonPropertyName("phonetics")]
    public string Phonetics { get; set; } = string.Empty;

    [JsonPropertyName("meanings")]
    public List<MeaningJson> Meanings { get; set; } = [];

    [JsonPropertyName("collocations")]
    public List<string> Collocations { get; set; } = [];

    [JsonPropertyName("examples")]
    public List<WordExampleJsonDto> Examples { get; set; } = [];

    [JsonPropertyName("special_usage")]
    public string SpecialUsage { get; set; } = string.Empty;

    [JsonPropertyName("difficulty_level")]
    public string DifficultyLevel { get; set; } = "intermediate";

    [JsonPropertyName("cefr_level")]
    public string CefrLevel { get; set; } = "B1";

    public DefinitionResponse ToResponse(string word, string? context)
    {
        var contextual = context ?? string.Empty;
        var meanings = Meanings.Count > 0
            ? Meanings.Select(item => new Meaning(
                item.Definition,
                item.IsContextual,
                item.IsContextual ? contextual : string.Empty)).ToList()
            : [new Meaning(string.Empty, true, contextual)];

        return new DefinitionResponse(
            word,
            Phonetics,
            meanings,
            Collocations,
            Examples.Select(MapExample).Where(item => item is not null).Cast<WordExample>().ToList(),
            SpecialUsage,
            MapDifficulty(DifficultyLevel),
            MapCefr(CefrLevel));
    }

    private static WordExample? MapExample(WordExampleJsonDto item)
    {
        if (string.IsNullOrWhiteSpace(item.Sentence))
        {
            return null;
        }

        var kind = item.Kind.Trim().ToLowerInvariant() switch
        {
            "general" => WordExampleKind.General,
            _ => WordExampleKind.Contextual
        };

        return new WordExample(
            kind,
            item.Sentence.Trim(),
            string.IsNullOrWhiteSpace(item.Explanation) ? item.Sentence.Trim() : item.Explanation.Trim());
    }

    private static DifficultyLevel MapDifficulty(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "advanced" => global::NextWord.Domain.Enums.DifficultyLevel.Advanced,
            "basic" => global::NextWord.Domain.Enums.DifficultyLevel.Basic,
            _ => global::NextWord.Domain.Enums.DifficultyLevel.Intermediate
        };

    private static CefrLevel MapCefr(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "A1" => global::NextWord.Domain.Enums.CefrLevel.A1,
            "A2" => global::NextWord.Domain.Enums.CefrLevel.A2,
            "B2" => global::NextWord.Domain.Enums.CefrLevel.B2,
            "C1" => global::NextWord.Domain.Enums.CefrLevel.C1,
            "C2" => global::NextWord.Domain.Enums.CefrLevel.C2,
            _ => global::NextWord.Domain.Enums.CefrLevel.B1
        };
}

internal sealed class MeaningJson
{
    [JsonPropertyName("definition")]
    public string Definition { get; set; } = string.Empty;

    [JsonPropertyName("is_contextual")]
    public bool IsContextual { get; set; } = true;
}

internal sealed class WordExampleJsonDto
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "contextual";

    [JsonPropertyName("sentence")]
    public string Sentence { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;
}
