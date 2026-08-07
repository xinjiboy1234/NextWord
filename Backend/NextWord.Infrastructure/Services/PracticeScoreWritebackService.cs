using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// T-022/T-047：日常练习评分的小步回写 Score 内核。
/// Writing ← 造句/自由表达（T-022）；Vocabulary ← 背词考察（T-047）；Reading ← 阅读完成（T-047）。
/// 只由用户主动练习的端点调用；测评（AssessmentService）与挑战（ChallengeService）
/// 复用 SentenceService.RateAsync 但不经过本服务，绝不触发该 delta。
/// Writing 口径：observed = MapSentenceToScore(四维均分)；delta = clamp(round((observed - current) * 0.1), -2, +2)。
/// T-047 口径（DESIGN-vr-score-writeback §2）：
///   Vocabulary：observed = 考察词有效难度分（EffectiveDifficultyCalculator 0-100）× 表现系数（对 1.0/错 0.3），
///     delta = clamp(round((observed - current) * 0.05), -1, +1)（背词高频，步长比 Writing 更缓防刷）；
///   Reading：observed = 文章难度分 × 查词修正系数（查词率 ≤5% → 1.0，每超 5% 减 0.1，下限 0.5），
///     delta = clamp(round((observed - current) * 0.1), -2, +2)。
/// delta = 0 也照常落幂等记录（LearningEvents），防止重放时重复加分。
/// </summary>
public sealed class PracticeScoreWritebackService(
    IScoreProfileService scoreProfile,
    IAssessmentScoringService scoring,
    ApplicationDbContext db)
{
    // T-047 口径常量（DESIGN-vr-score-writeback §3：留常量，后续按仿真校准）
    private const double VocabStepFactor = 0.05;
    private const int VocabDeltaClamp = 1;
    private const double WrongAnswerFactor = 0.3;
    private const double ReadingStepFactor = 0.1;
    private const int ReadingDeltaClamp = 2;
    private const double LookupFreeRate = 0.05;
    private const double LookupRateStep = 0.05;
    private const double LookupCoefficientFloor = 0.5;

    public async Task<WritingScoreChange> ApplySentenceAsync(Guid userId, SentenceLog log, CancellationToken cancellationToken)
    {
        var average = (log.GrammarScore + log.NaturalScore + log.VocabularyScore + log.RelevanceScore) / 4.0;
        var observed = scoring.MapSentenceToScore(average);
        return await ApplyWritingStepAsync(userId, observed, "SentencePractice", $"sentence-score:{log.Id}", cancellationToken);
    }

    public async Task<WritingScoreChange> ApplyFreeExpressionAsync(Guid userId, FreeExpressionLog log, CancellationToken cancellationToken)
    {
        // AiScore = 四维总分 * 5，与 MapSentenceToScore(四维均分) 数值同口径
        return await ApplyWritingStepAsync(userId, log.AiScore, "FreeExpressionPractice", $"freeexpr-score:{log.Id}", cancellationToken);
    }

    /// <summary>T-047：背词考察提交后小步回写 Vocabulary 维（幂等键 vocab-score:{logId}）。</summary>
    public async Task<VocabularyScoreChange> ApplyVocabularyAsync(Guid userId, WordLearningLog log, CancellationToken cancellationToken)
    {
        var word = await db.Words.AsNoTracking()
            .Include(item => item.LlmAnnotation)
            .FirstOrDefaultAsync(item => item.Id == log.WordId, cancellationToken);
        var relationship = await db.UserWordRelationships.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId && item.WordId == log.WordId, cancellationToken);

        var effective = EffectiveDifficultyCalculator.Compute(
            word?.LlmAnnotation?.IntrinsicScore,
            word is null ? null : LegacyScoreHelper.FromDifficulty(word.DifficultyLevel),
            relationship,
            null);
        var observed = effective.Score * (log.IsCorrect ? 1.0 : WrongAnswerFactor);

        var before = (await scoreProfile.GetScoresAsync(userId, cancellationToken)).Vocabulary ?? 0;
        var delta = Math.Clamp(
            (int)Math.Round((observed - before) * VocabStepFactor, MidpointRounding.AwayFromZero),
            -VocabDeltaClamp, VocabDeltaClamp);
        var result = await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "VocabularyPractice",
                null,
                new ProfileScoreDelta(delta, null, null, null),
                $"vocab-score:{log.Id}"),
            cancellationToken);
        return new VocabularyScoreChange(before, result.Scores.Vocabulary ?? before);
    }

    /// <summary>T-047：阅读完成后小步回写 Reading 维（幂等键 reading-score:{logId}）。</summary>
    public async Task<ReadingScoreChange> ApplyReadingAsync(Guid userId, ReadingLog log, CancellationToken cancellationToken)
    {
        var article = await db.Articles.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == log.ArticleId, cancellationToken);
        var articleScore = article is null ? 50 : LegacyScoreHelper.FromDifficulty(article.DifficultyLevel);
        var lookupRate = log.LookupCount / (double)Math.Max(article?.WordCount ?? 1, 1);
        var observed = articleScore * LookupCoefficient(lookupRate);

        var before = (await scoreProfile.GetScoresAsync(userId, cancellationToken)).Reading ?? 0;
        var delta = Math.Clamp(
            (int)Math.Round((observed - before) * ReadingStepFactor, MidpointRounding.AwayFromZero),
            -ReadingDeltaClamp, ReadingDeltaClamp);
        var result = await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "ReadingPractice",
                null,
                new ProfileScoreDelta(null, delta, null, null),
                $"reading-score:{log.Id}"),
            cancellationToken);
        return new ReadingScoreChange(before, result.Scores.Reading ?? before);
    }

    /// <summary>T-047：查词率修正系数——≤5% 不降权，每超 5% 减 0.1，下限 0.5（查词是正常学习行为，只降权不惩罚）。</summary>
    public static double LookupCoefficient(double lookupRate)
    {
        if (lookupRate <= LookupFreeRate)
        {
            return 1.0;
        }

        // 减 1e-9 抵消浮点误差：查词率恰在档位边界（如 10%）只降一档
        var steps = (int)Math.Ceiling((lookupRate - LookupFreeRate) / LookupRateStep - 1e-9);
        return Math.Max(LookupCoefficientFloor, 1.0 - 0.1 * steps);
    }

    private async Task<WritingScoreChange> ApplyWritingStepAsync(
        Guid userId, int observed, string source, string idempotencyKey, CancellationToken cancellationToken)
    {
        var before = (await scoreProfile.GetScoresAsync(userId, cancellationToken)).Writing ?? 0;
        var delta = Math.Clamp((int)Math.Round((observed - before) * 0.1, MidpointRounding.AwayFromZero), -2, 2);
        var result = await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                source,
                null,
                new ProfileScoreDelta(null, null, delta, null),
                idempotencyKey),
            cancellationToken);
        return new WritingScoreChange(before, result.Scores.Writing ?? before);
    }
}

/// <summary>一次练习评分引起的 Writing 分前后值（供响应带出展示）。</summary>
public sealed record WritingScoreChange(int Before, int After);

/// <summary>T-047：一次背词考察引起的 Vocabulary 分前后值。</summary>
public sealed record VocabularyScoreChange(int Before, int After);

/// <summary>T-047：一次阅读完成引起的 Reading 分前后值。</summary>
public sealed record ReadingScoreChange(int Before, int After);
