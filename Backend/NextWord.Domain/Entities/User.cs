namespace NextWord.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<UserProgress> ProgressRecords { get; set; } = [];
    public List<UserWordRelationship> WordRelationships { get; set; } = [];
    public List<WordLearningLog> LearningLogs { get; set; } = [];
}
