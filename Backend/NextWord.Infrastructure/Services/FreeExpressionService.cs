using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class FreeExpressionService(ApplicationDbContext db, ILLMProvider llm) : IFreeExpressionService
{
    public async Task<FreeExpressionLog> RateAsync(Guid userId, string userText, string userLevel, CancellationToken cancellationToken)
    {
        var rating = await llm.RateSentenceAsync(new SentenceRatingRequest(
            userText.Trim(),
            "free expression",
            "free-expression",
            string.IsNullOrWhiteSpace(userLevel) ? "A2" : userLevel.Trim(),
            new LlmRequestOptions("feedback-rich", "free_expression_feedback")), cancellationToken);

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
        return log;
    }
}
