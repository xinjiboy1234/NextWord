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
    Task<ChallengePack> StartChallengeAsync(Guid userId, bool confirmationChallenge, CancellationToken cancellationToken);
    Task<ChallengeRecord> SubmitChallengeAsync(Guid userId, ChallengeType type, double vocabScore, double sentenceScore, double readingScore, bool confirmationChallenge, CancellationToken cancellationToken);
    Task<IReadOnlyList<ChallengeRecord>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken);
}
