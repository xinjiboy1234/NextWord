using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IAssessmentScoringService
{
    // ── T-004：表达力综合分（产出题四维加权，规则引擎权威）──
    double ScoreProductionDimensions(int grammar, int natural, int vocabulary, int relevance);
    CefrLevel MapExpressionScore(double compositeScore);

    // ── T-004：自适应分块决策 ──
    BandMove DecideBandMove(double blockExpressionScore);
    bool ShouldConverge(int completedBlocks, BandMove lastMove);

    // ── T-042：识别防伪闸（定级后一次性矫正，识别样本缺失传 null 不矫正）──
    (CefrLevel Level, bool Adjusted) ApplyRecognitionGuard(CefrLevel expressionLevel, CefrLevel? vocabReferenceLevel);

    // ── T-042：矫正传导——指定 CEFR 档的分数带上限（含），供分数先验 clamp ──
    int GetBandScoreCeiling(CefrLevel level);

    // ── 识别题参考映射（不计入主定级）；MapXxxToScore 同时被挑战流沿用 ──
    CefrLevel MapVocabAccuracy(double accuracyPercent);
    CefrLevel MapReadingAccuracy(double accuracyPercent, int lookupCount, int wordCount);
    int MapVocabToScore(double accuracyPercent);
    int MapSentenceToScore(double averageScore);
    int MapReadingToScore(double accuracyPercent, int lookupCount, int wordCount);
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
    Task<AssessmentBlockResponse> GetNextBlockAsync(Guid assessmentId, CancellationToken cancellationToken);
    Task<AssessmentBlockResult> SubmitBlockAsync(Guid assessmentId, int blockIndex, IReadOnlyList<AssessmentAnswerItem> answers, CancellationToken cancellationToken);
    /// <summary>T-065：异步评分——提交答案立即返回（入队后台评分任务）。</summary>
    Task SubmitBlockForScoringAsync(Guid assessmentId, int blockIndex, IReadOnlyList<AssessmentAnswerItem> answers, CancellationToken cancellationToken);
    /// <summary>T-065：后台评分任务执行（评分核心 + 收敛定级）。</summary>
    Task ScoreBlockJobAsync(Guid assessmentId, int blockIndex, CancellationToken cancellationToken);
    Task<Assessment?> GetAsync(Guid assessmentId, CancellationToken cancellationToken);
    /// <summary>T-054：当前用户测评历史列表（按开始时间倒序，只读无分页）。</summary>
    Task<IReadOnlyList<AssessmentListItem>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);
    Task SkipInitialAsync(Guid userId, CancellationToken cancellationToken);
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
    int? ReadingSelectedIndex,
    int LookupCount)
{
    /// <summary>T-035：阅读 3 题作答；为 null 时回退单题 <see cref="ReadingSelectedIndex"/>（旧客户端/旧会话）。</summary>
    public IReadOnlyList<int>? ReadingSelectedIndexes { get; init; }
}

public sealed record ChallengeStartResponse(Guid ChallengeSessionId, ChallengePackClientView Pack);

public sealed record ChallengePackClientView(
    IReadOnlyList<VocabQuizQuestionClient> Vocabulary,
    SentenceQuizQuestion Sentence,
    IReadOnlyList<ReadingQuizQuestionClient> Readings,
    string AttemptedLevel);

public sealed record VocabQuizQuestionClient(string Word, IReadOnlyList<string> Options, string Difficulty);
public sealed record ReadingQuizQuestionClient(Guid ArticleId, string Question, IReadOnlyList<string> Options, string ArticleExcerpt);

/// <summary>
/// T-035：Daily 通过后带规则点评（Feedback，未通过为 null）；PassCount 为累计通过次数（Daily 未通过也返回当前计数；确认挑战为 null）。
/// </summary>
public sealed record ChallengeSubmitResponse(
    bool Passed,
    double TotalScore,
    int VocabularyScore,
    int WritingScore,
    int ReadingScore,
    long? EvaluationReportId,
    string? Feedback,
    int? PassCount);
