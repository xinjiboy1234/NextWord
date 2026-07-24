using NextWord.Domain.Enums;
using NextWord.Domain.Scenarios;
using NextWord.Infrastructure.Data;

namespace NextWord.UnitTests;

/// <summary>
/// 内置词表验收（设计方案 §4 / §6-3）：每子场景 >=60 有效词、core 桶 >=500、
/// core_verb+connector 占比 >=40%、utility=low 不入库。
/// </summary>
public sealed class WordlistSeedTests
{
    private static readonly IReadOnlyList<WordlistSeedData.WordlistEntry> Entries = WordlistSeedData.LoadEntries();

    [Fact]
    public void Wordlist_MeetsScaleTargets()
    {
        Assert.True(Entries.Count >= 1500, $"total {Entries.Count} < 1500");

        foreach (var sub in ScenarioTaxonomy.All)
        {
            var count = Entries.Count(entry => entry.Scenarios.Contains(sub.Key));
            Assert.True(count >= 60, $"scenario {sub.Key} has {count} words (< 60)");
        }

        var coreCount = Entries.Count(entry => entry.Scenarios.Count == 0);
        Assert.True(coreCount >= 500, $"core bucket {coreCount} < 500");

        var expressive = Entries.Count(entry => entry.Role is "core_verb" or "connector");
        Assert.True((double)expressive / Entries.Count >= 0.40,
            $"core_verb+connector ratio {(double)expressive / Entries.Count:P1} < 40%");
    }

    [Fact]
    public void Wordlist_EntriesAreWellFormed()
    {
        var lemmas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.Lemma));
            Assert.True(lemmas.Add(entry.Lemma), $"duplicate lemma: {entry.Lemma}");
            Assert.NotEmpty(entry.Meanings);
            Assert.NotEqual("low", entry.Utility);
            Assert.Contains(entry.Utility, new[] { "high", "medium" });
            Assert.Contains(entry.Role, new[] { "core_verb", "connector", "scene_noun", "phrase_pattern" });
            Assert.Contains(entry.Cefr, new[] { "A1", "A2", "B1", "B2", "C1", "C2" });
            Assert.True(entry.Scenarios.Count <= 3, $"{entry.Lemma} has {entry.Scenarios.Count} scenarios");
            Assert.All(entry.Scenarios, key => Assert.True(ScenarioTaxonomy.IsSubScenarioKey(key), $"bad key {key}"));
        }
    }

    [Fact]
    public void ToWord_MapsEntryToAnnotatedWord()
    {
        var entry = Entries.First(item => item.Scenarios.Count > 0 && item.Examples.Count > 0);

        var word = WordlistSeedData.ToWord(entry);

        Assert.Equal(entry.Lemma, word.Lemma);
        Assert.Equal(entry.Meanings, word.Meanings);
        Assert.Equal(Infrastructure.Services.ScenarioAnnotationWorker.CurrentVersion, word.ScenarioAnnotationVersion);
        Assert.NotNull(word.Utility);
        Assert.NotNull(word.Role);
        Assert.Equal(entry.Scenarios.Order(), word.Scenarios.Select(item => item.ScenarioKey).Order());
        var expectedDifficulty = word.CefrLevel is CefrLevel.A1 or CefrLevel.A2
            ? DifficultyLevel.Basic
            : word.CefrLevel is CefrLevel.B1 or CefrLevel.B2
                ? DifficultyLevel.Intermediate
                : DifficultyLevel.Advanced;
        Assert.Equal(expectedDifficulty, word.DifficultyLevel);
    }
}
