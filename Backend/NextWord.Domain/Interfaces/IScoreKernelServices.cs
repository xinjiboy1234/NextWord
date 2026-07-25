using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IBackgroundJobService
{
    Task<long> EnqueueAsync(string jobType, string payloadJson, string idempotencyKey, CancellationToken cancellationToken);
    Task ProcessPendingAsync(CancellationToken cancellationToken);
}

public interface IEvaluationReportService
{
    Task<long> EnqueueForUserAsync(Guid userId, string triggerType, Guid? assessmentId, CancellationToken cancellationToken);
    Task ProcessJobAsync(BackgroundJob job, CancellationToken cancellationToken);
}

public interface IReadingLookupService
{
    Task<ReadingLookupResponse> LookupAsync(Guid userId, ReadingLookupRequest request, CancellationToken cancellationToken);
}

public sealed record ReadingLookupRequest(string Word, string Sentence, Guid? ArticleId);

public sealed record WordExampleDto(
    string Kind,
    string Sentence,
    string Explanation)
{
    public static WordExampleDto FromModel(WordExample example) => new(
        example.Kind.ToString().ToLowerInvariant(),
        example.Sentence,
        example.Explanation);
}

public sealed record ReadingLookupResponse(
    string Word,
    string ContextDefinition,
    int? IntrinsicScore,
    int? PersonalDifficulty,
    double EstimatedKnownRate,
    string? Phonetic,
    bool Offline,
    double? Confidence,
    IReadOnlyList<SearchResultItem>? Sources,
    string? SpecialUsage,
    IReadOnlyList<WordExampleDto>? Examples,
    bool FromCache);

public sealed record SearchResultItem(string Title, string Url, string Snippet);

public interface IDailyWordSelectionService
{
    Task<IReadOnlyList<DailyWordItem>> GetDailyAsync(Guid userId, int count, CancellationToken cancellationToken);
}

public sealed record DailyWordItem(
    Guid Id,
    string Lemma,
    IReadOnlyList<string> Meanings,
    int EffectiveDifficulty,
    bool IsWeak,
    string? Phonetics,
    /// <summary>T-006：来自当日 LearningPlan（否则为难度带回退选词）。</summary>
    bool FromPlan = false,
    /// <summary>T-006：超带「接触词」，只进背词识别队列，认识即可。</summary>
    bool IsExposure = false);

public interface IUserFeedbackService
{
    Task SubmitAsync(Guid userId, string feedbackType, string targetWord, string? contextJson, CancellationToken cancellationToken);
}
