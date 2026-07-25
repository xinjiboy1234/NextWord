using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class SentenceService(
    ApplicationDbContext db,
    IUserLlmProviderFactory llmFactory,
    IOptions<LlmSentenceRatingOptions> sentenceRatingOptions,
    ILearningPlanService learningPlans,
    IScoreProfileService scoreProfile) : ISentenceService
{
    public async Task<IReadOnlyList<Sentence>> GetPromptsAsync(int count, CancellationToken cancellationToken)
    {
        return await db.Sentences
            .AsNoTracking()
            .Include(sentence => sentence.Word)
            .OrderBy(sentence => sentence.DifficultyLevel)
            .ThenBy(sentence => sentence.TargetWord)
            .Take(Math.Clamp(count, 1, 30))
            .ToListAsync(cancellationToken);
    }

    public async Task<SentencePromptBatch> GetPersonalizedPromptsAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        count = Math.Clamp(count, 1, 30);

        // 优先执行当日 Plan 的造句目标（带内、主攻场景优先）
        var active = await learningPlans.GetActiveAsync(userId, cancellationToken);
        if (active is not null && active.DayIndex < active.Content.Days.Count)
        {
            var targets = active.Content.Days[active.DayIndex].SentenceTargets;
            if (targets.Count > 0)
            {
                var lemmas = targets.Select(target => target.ToLowerInvariant()).ToList();
                var words = await db.Words.AsNoTracking()
                    .Where(word => lemmas.Contains(word.Lemma))
                    .ToListAsync(cancellationToken);
                var scene = active.Content.FocusScenarios.FirstOrDefault() ?? "life";
                var prompts = targets
                    .Select(target => words.FirstOrDefault(word => word.Lemma == target.ToLowerInvariant()))
                    .Where(word => word is not null)
                    .Select(word => new Sentence
                    {
                        WordId = word!.Id,
                        TargetWord = word.Lemma,
                        Content = word.ExampleSentences.FirstOrDefault() ?? string.Empty,
                        DifficultyLevel = word.DifficultyLevel,
                        CefrLevel = word.CefrLevel,
                        Scene = scene
                    })
                    .ToList();

                if (prompts.Count < count)
                {
                    prompts.AddRange(await GetBandPromptsAsync(userId, count - prompts.Count, lemmas, cancellationToken));
                }

                if (prompts.Count > 0)
                {
                    return new SentencePromptBatch(prompts.Take(count).ToList(), true);
                }
            }
        }

        // 回退：产出任务选词限水平带内（VISION §4.3），带内无题可用时退回既有出题
        var bandPrompts = await GetBandPromptsAsync(userId, count, [], cancellationToken);
        return bandPrompts.Count > 0
            ? new SentencePromptBatch(bandPrompts, false)
            : new SentencePromptBatch(await GetPromptsAsync(count, cancellationToken), false);
    }

    /// <summary>
    /// 带内约束出题：目标词 CEFR 与水平带一致（与测评词池口径一致；词库词多数无
    /// IntrinsicScore 标注，intrinsic 带会大面积落空），带池不足向下一带补充。
    /// </summary>
    private async Task<List<Sentence>> GetBandPromptsAsync(
        Guid userId, int count, IReadOnlyList<string> excludeLemmas, CancellationToken cancellationToken)
    {
        if (count <= 0)
        {
            return [];
        }

        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
        var userCefr = Enum.TryParse<CefrLevel>(scores.CefrDisplay, out var parsed) ? parsed : CefrLevel.A2;

        var sentences = await db.Sentences.AsNoTracking()
            .Include(sentence => sentence.Word)
            .ToListAsync(cancellationToken);

        var candidates = sentences
            .Where(sentence => !excludeLemmas.Contains(sentence.TargetWord.ToLowerInvariant()))
            .ToList();
        var inBand = candidates
            .Where(sentence => (sentence.Word?.CefrLevel ?? sentence.CefrLevel) == userCefr)
            .ToList();
        if (inBand.Count < count && userCefr > CefrLevel.A1)
        {
            inBand.AddRange(candidates.Where(sentence => (sentence.Word?.CefrLevel ?? sentence.CefrLevel) == userCefr - 1));
        }

        return inBand
            .OrderBy(_ => Random.Shared.Next())
            .Take(count)
            .ToList();
    }

    public async Task<SentenceLog> RateAsync(
        Guid userId,
        Guid? wordId,
        string targetWord,
        string userSentence,
        string scene,
        string userLevel,
        CancellationToken cancellationToken)
    {
        var llm = await llmFactory.GetForUserAsync(userId, cancellationToken);
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            null,
            sentenceRatingOptions.Value.ExplanationLanguage);
        var rating = await llm.RateSentenceAsync(new SentenceRatingRequest(
            userSentence.Trim(),
            targetWord.Trim(),
            string.IsNullOrWhiteSpace(scene) ? "life" : scene.Trim(),
            string.IsNullOrWhiteSpace(userLevel) ? "A2" : userLevel.Trim(),
            new LlmRequestOptions("grading-stable", "sentence_rating"),
            explanationLanguage), cancellationToken);

        var log = new SentenceLog
        {
            UserId = userId,
            WordId = wordId,
            TargetWord = targetWord.Trim().ToLowerInvariant(),
            Scene = string.IsNullOrWhiteSpace(scene) ? "life" : scene.Trim(),
            UserSentence = userSentence.Trim(),
            AiRevision = rating.AiRevision,
            GrammarScore = ClampScore(rating.GrammarScore),
            NaturalScore = ClampScore(rating.NaturalScore),
            VocabularyScore = ClampScore(rating.VocabularyScore),
            RelevanceScore = ClampScore(rating.RelevanceScore),
            OverallGrade = NormalizeGrade(rating.OverallGrade),
            ErrorTags = rating.ErrorAnalysis.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToList(),
            DifficultyLevel = rating.DifficultyLevel,
            Suggestion = rating.Suggestion
        };

        db.SentenceLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    private static int ClampScore(int score) => Math.Clamp(score, 0, 5);

    private static string NormalizeGrade(string grade)
    {
        var value = grade.Trim().ToUpperInvariant();
        return value is "A" or "B" or "C" or "D" ? value : "C";
    }
}
