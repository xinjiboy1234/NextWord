using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IAssessmentScoringService
{
    CefrLevel MapVocabAccuracy(double accuracyPercent);
    CefrLevel MapSpellingAccuracy(double accuracyPercent);
    CefrLevel MapSentenceAverage(double averageScore);
    CefrLevel MapReadingAccuracy(double accuracyPercent, int lookupCount, int wordCount);
    FinalLevelResult CalculateFinalLevel(StepScoreResult vocab, StepScoreResult spelling, StepScoreResult sentence, StepScoreResult reading);
    int MapVocabToScore(double accuracyPercent);
    int MapSpellingToScore(double accuracyPercent);
    int MapSentenceToScore(double averageScore);
    int MapReadingToScore(double accuracyPercent, int lookupCount, int wordCount);
    FinalScoreResult CalculateFinalScores(StepScoreResult vocab, StepScoreResult spelling, StepScoreResult sentence, StepScoreResult reading);
}

public interface IChallengePackGenerator
{
    Task<ChallengePack> GenerateAsync(Guid userId, CefrLevel currentLevel, bool confirmationChallenge, CancellationToken cancellationToken);
}

public interface ILevelEngine
{
    UpgradeCheckResult EvaluateUpgradeCandidate(UserProgress progress, IReadOnlyList<ChallengeRecord> recentChallenges);
    CefrLevel GetNextLevel(CefrLevel current);
    CefrLevel GetPreviousLevel(CefrLevel current);
    Task ApplyLevelChangeAsync(Guid userId, CefrLevel from, CefrLevel to, LevelChangeReason reason, CancellationToken cancellationToken);
}

public interface IAssessmentService
{
    Task<Assessment> StartInitialAsync(Guid userId, CancellationToken cancellationToken);
    Task<object> GetStepQuestionsAsync(Guid assessmentId, AssessmentStepType step, CancellationToken cancellationToken);
    Task<StepScoreResult> SubmitStepAsync(Guid assessmentId, AssessmentStepType step, string answersJson, CancellationToken cancellationToken);
    Task<FinalLevelResult?> CompleteInitialAsync(Guid assessmentId, CancellationToken cancellationToken);
    Task<Assessment?> GetAsync(Guid assessmentId, CancellationToken cancellationToken);
}

public interface IChallengeService
{
    Task<ChallengeStartResponse> StartChallengeAsync(Guid userId, bool confirmationChallenge, CancellationToken cancellationToken);
    Task<ChallengeSubmitResponse> SubmitChallengeAsync(Guid userId, ChallengeSubmitRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChallengeRecord>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken);
}

public sealed record ChallengeSubmitRequest(
    Guid ChallengeSessionId,
    ChallengeType ChallengeType,
    IReadOnlyList<int> VocabAnswers,
    string SentenceAnswer,
    string TargetWord,
    string Scene,
    Guid? SentenceWordId,
    int ReadingSelectedIndex,
    int LookupCount);

public sealed record ChallengeStartResponse(Guid ChallengeSessionId, ChallengePackClientView Pack);

public sealed record ChallengePackClientView(
    IReadOnlyList<VocabQuizQuestionClient> Vocabulary,
    SentenceQuizQuestion Sentence,
    ReadingQuizQuestionClient Reading,
    string AttemptedLevel);

public sealed record VocabQuizQuestionClient(string Word, IReadOnlyList<string> Options, string Difficulty);
public sealed record ReadingQuizQuestionClient(Guid ArticleId, string Question, IReadOnlyList<string> Options, string ArticleExcerpt);

public sealed record ChallengeSubmitResponse(
    bool Passed,
    double TotalScore,
    int VocabularyScore,
    int WritingScore,
    int ReadingScore,
    long? EvaluationReportId);
