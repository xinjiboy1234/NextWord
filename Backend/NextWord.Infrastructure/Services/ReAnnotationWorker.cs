using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ReAnnotationWorker(
    ApplicationDbContext db,
    ILLMProvider llm)
{
    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.PayloadJson);
        var feedbackId = doc.RootElement.GetProperty("feedbackId").GetInt64();
        var feedback = await db.UserFeedbacks.FirstOrDefaultAsync(item => item.Id == feedbackId, cancellationToken)
            ?? throw new InvalidOperationException("Feedback not found.");

        if (!string.Equals(feedback.FeedbackType, "DefinitionWrong", StringComparison.OrdinalIgnoreCase))
        {
            feedback.Status = "Skipped";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var lemma = feedback.TargetWord.Trim().ToLowerInvariant();
        var word = await db.Words
            .Include(item => item.LlmAnnotation)
            .FirstOrDefaultAsync(item => item.Lemma == lemma, cancellationToken);

        if (word is null)
        {
            feedback.Status = "NoWord";
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (word.LlmAnnotation is not null)
        {
            word.LlmAnnotation.IsCurrent = false;
        }

        var rating = await llm.RateDifficultyAsync(new(ItemType.Word, lemma), cancellationToken);
        var annotation = new WordDifficultyAnnotation
        {
            WordId = word.Id,
            DifficultyLevel = rating.DifficultyLevel,
            CefrLevel = rating.CefrLevel,
            Reason = "ReAnnotation from user feedback",
            RecommendedAction = RecommendedAction.ReviewLater,
            Confidence = Math.Max(0.3, (word.LlmAnnotation?.Confidence ?? 0.5) - 0.1),
            ModelProfileId = "reannotation-v1",
            // T-061：内在难度分按 CEFR 六档映射（比 legacy 三档更细，与带内选词口径一致）
            IntrinsicScore = LegacyScoreHelper.FromCefr(rating.CefrLevel),
            Version = (word.LlmAnnotation?.Version ?? 0) + 1,
            IsCurrent = true,
            PromptVersion = "feedback-reannotation",
            SchemaVersion = 1
        };

        db.WordDifficultyAnnotations.Add(annotation);
        word.LlmAnnotationId = annotation.Id;
        word.LlmAnnotation = annotation;
        word.DifficultyLevel = rating.DifficultyLevel;
        word.CefrLevel = rating.CefrLevel;

        feedback.Status = "Processed";
        await db.SaveChangesAsync(cancellationToken);
    }
}
