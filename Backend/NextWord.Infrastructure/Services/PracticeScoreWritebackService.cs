using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// T-022：日常造句/自由表达评分的小步回写 Score 内核（Writing 维）。
/// 只由用户主动练习的评分端点调用；测评（AssessmentService）与挑战（ChallengeService）
/// 复用 SentenceService.RateAsync 但不经过本服务，绝不触发该 delta。
/// 口径：observed = MapSentenceToScore(四维均分)；delta = clamp(round((observed - current) * 0.1), -2, +2)，
/// delta = 0 也照常落幂等记录（LearningEvents），防止重放时重复加分。
/// </summary>
public sealed class PracticeScoreWritebackService(
    IScoreProfileService scoreProfile,
    IAssessmentScoringService scoring)
{
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
