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
        // T-037：不再传「free expression」字面量——qwen 把它当写作主题，高质量段落被判 off-topic；
        // 传中性主题描述 + IsFreeExpression 标记，prompt 走自由表达变体，相关性按「围绕日常场景/主题」评
        var rating = await llm.RateSentenceAsync(new SentenceRatingRequest(
            userText.Trim(),
            "日常自由表达",
            "daily-life",
            ratingBand,
            new LlmRequestOptions("feedback-rich", "free_expression_feedback"),
            explanationLanguage,
            IsFreeExpression: true), cancellationToken);

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
        var graduatedWords = await GraduatedSpontaneousUseAsync(userId, log, rating.VocabularyScore, cancellationToken);
        return new FreeExpressionRatingResult(log, graduatedWords);
    }

    /// <summary>
    /// T-014（DESIGN-word-lifecycle §2）：统一命中口径（T-040 TargetWordMatcher：单词词边界、多词短语连续词序列）做词级判定——
    /// 当次评分达标时，文中出现的 prompted_use 候选池词毕业（spontaneous_use），
    /// 留痕所在 FreeExpressionLog Id；评分不达标或词未出现不毕业。
    /// T-044 评分达标口径放宽：整篇 C 及以上且词汇维 ≥3 即达标（D 档或词汇维 ≤2 仍不毕业，防烂底线不动）；
    /// 造句确认门槛（A/B）与 TargetWordMatcher 命中口径不动。
    /// T-034：返回本次毕业的词 lemma 列表（无毕业返回空列表）。
    /// </summary>
    private async Task<IReadOnlyList<string>> GraduatedSpontaneousUseAsync(Guid userId, FreeExpressionLog log, int vocabularyScore, CancellationToken cancellationToken)
    {
        if (!WordLifecycleService.IsGraduationGrade(log.OverallGrade) || vocabularyScore < 3)
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

        var now = DateTimeOffset.UtcNow;
        var graduated = new List<string>();
        foreach (var relationship in candidates)
        {
            if (relationship.Word is not null && TargetWordMatcher.IsHit(relationship.Word.Lemma, log.UserText))
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
