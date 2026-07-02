namespace NextWord.Domain.Models;

public sealed class ScoreMappingOptions
{
    public const string SectionName = "ScoreMapping";

    public List<ScoreBand> CefrBands { get; set; } =
    [
        new() { Min = 0, Max = 20, Label = "A1" },
        new() { Min = 20, Max = 35, Label = "A2" },
        new() { Min = 35, Max = 50, Label = "B1" },
        new() { Min = 50, Max = 70, Label = "B2" },
        new() { Min = 70, Max = 85, Label = "C1" },
        new() { Min = 85, Max = 100, Label = "C2" }
    ];

    public List<ScoreBand> DifficultyBuckets { get; set; } =
    [
        new() { Min = 0, Max = 35, Label = "Basic" },
        new() { Min = 35, Max = 70, Label = "Intermediate" },
        new() { Min = 70, Max = 101, Label = "Advanced" }
    ];
}

public sealed class ScoreBand
{
    public int Min { get; set; }
    public int Max { get; set; }
    public string Label { get; set; } = string.Empty;
}
