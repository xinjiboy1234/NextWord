using NextWord.Domain.Entities;
using NextWord.Domain.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextWord.Domain.Services;

public static class WordExampleJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string Serialize(IReadOnlyList<WordExample> examples) =>
        JsonSerializer.Serialize(examples, Options);

    public static IReadOnlyList<WordExample> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<WordExample>>(json, Options) ?? [];
    }

    public static IReadOnlyList<WordExample> FromKeyVocabItem(KeyVocabItem item)
    {
        var examples = new List<WordExample>(2);
        if (item.UsageExample is not null)
        {
            examples.Add(item.UsageExample with { Kind = WordExampleKind.Contextual });
        }

        if (item.GeneralExample is not null)
        {
            examples.Add(item.GeneralExample with { Kind = WordExampleKind.General });
        }

        return examples;
    }

    public static bool IsEnriched(ArticleVocabMapping mapping) =>
        !string.IsNullOrWhiteSpace(mapping.Phonetics) && mapping.ExamplesJson is not null;
}
