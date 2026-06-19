using NextWord.Domain.Enums;

namespace NextWord.Domain.Entities;

public sealed class AssessmentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssessmentId { get; set; }
    public AssessmentStepType Step { get; set; }
    public string QuestionType { get; set; } = string.Empty;
    public string QuestionsJson { get; set; } = "[]";
    public string AnswersJson { get; set; } = "[]";
    public string ScoresJson { get; set; } = "{}";
    public Guid? ArticleId { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public Assessment? Assessment { get; set; }
}
