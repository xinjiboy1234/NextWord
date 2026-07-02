namespace NextWord.Domain.Models;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    public bool Enabled { get; set; } = true;
    public int MaxResults { get; set; } = 3;
}
