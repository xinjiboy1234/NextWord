using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class FreeExpressionService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    IOptions<LlmSentenceRatingOptions> sentenceRatingOptions,
    IScoreProfileService scoreProfile) : IFreeExpressionService
{
    public async Task<FreeExpressionRatingResult> RateAsync(Guid userId, string userText, string? userLevel, CancellationToken cancellationToken)
    {
        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            null,
            sentenceRatingOptions.Value.ExplanationLanguage);
        // T-027：评分尺子 = 用户当前水平带（UserProgress 投影，ScoreMapping 单一来源），与造句评分同口径
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var ratingBand = RatingBandResolver.Resolve(scores, userLevel);
        var rating = await llm.RateSentenceAsync(new SentenceRatingRequest(
            userText.Trim(),
            "free expression",
            "free-expression",
            ratingBand,
            new LlmRequestOptions("feedback-rich", "free_expression_feedback"),
            explanationLanguage), cancellationToken);

        var score = (rating.GrammarScore + rating.NaturalScore + rating.VocabularyScore + rating.RelevanceScore) * 5;
        var log = new FreeExpressionLog
        {
            UserId = userId,
            UserText = userText.Trim(),
            AiScore = Math.Clamp(score, 0, 100),
            OverallGrade = string.IsNullOrWhiteSpace(rating.OverallGrade) ? "C" : rating.OverallGrade.Trim().ToUpperInvariant(),
            AiRevision = rating.AiRevision,
            ErrorSentences = rating.ErrorAnalysis.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList(),
            Suggestions = [rating.Suggestion],
            DifficultyLevel = rating.DifficultyLevel
        };

        db.FreeExpressionLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        // T-014 自发使用毕业判定：自由表达（非指定目标词的产出）中自发使用候选池词且当次评分达标 → 毕业留痕
        // T-034：返回本次毕业词列表，随评分响应带给前端做毕业时刻提示
        var graduatedWords = await GraduatedSpontaneousUseAsync(userId, log, cancellationToken);
        return new FreeExpressionRatingResult(log, graduatedWords);
    }

    /// <summary>
    /// T-014（DESIGN-word-lifecycle §2）：复用 T-007 安全词检测的词边界分词口径做词级判定——
    /// 当次评分达标（A/B）时，文中出现的 prompted_use 候选池词毕业（spontaneous_use），
    /// 留痕所在 FreeExpressionLog Id；评分不达标或词未出现不毕业。
    /// T-034：返回本次毕业的词 lemma 列表（无毕业返回空列表）。
    /// </summary>
    private async Task<IReadOnlyList<string>> GraduatedSpontaneousUseAsync(Guid userId, FreeExpressionLog log, CancellationToken cancellationToken)
    {
        if (!WordLifecycleService.IsPassingGrade(log.OverallGrade))
        {
            return [];
        }

        var candidates = await db.UserWordRelationships
            .Include(item => item.Word)
            .Where(item => item.UserId == userId && item.LifecycleStage == WordLifecycleStage.PromptedUse)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var tokens = BottleneckScreeningService.Tokenize(log.UserText);
        var now = DateTimeOffset.UtcNow;
        var graduated = new List<string>();
        foreach (var relationship in candidates)
        {
            if (relationship.Word is not null && tokens.Contains(relationship.Word.Lemma))
            {
                WordLifecycleService.Graduate(relationship, log.Id, now);
                graduated.Add(relationship.Word.Lemma);
            }
        }

        if (graduated.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return graduated;
    }
}
