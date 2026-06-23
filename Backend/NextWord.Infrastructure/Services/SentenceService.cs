using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class SentenceService(ApplicationDbContext db, IUserLlmProviderFactory llmFactory) : ISentenceService
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
        var rating = await llm.RateSentenceAsync(new SentenceRatingRequest(
            userSentence.Trim(),
            targetWord.Trim(),
            string.IsNullOrWhiteSpace(scene) ? "life" : scene.Trim(),
            string.IsNullOrWhiteSpace(userLevel) ? "A2" : userLevel.Trim(),
            new LlmRequestOptions("grading-stable", "sentence_rating")), cancellationToken);

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
