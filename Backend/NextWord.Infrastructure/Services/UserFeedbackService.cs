using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class UserFeedbackService(
    ApplicationDbContext db,
    IUserRepository users,
    IBackgroundJobService backgroundJobs) : IUserFeedbackService
{
    public async Task SubmitAsync(Guid userId, string feedbackType, string targetWord, string? contextJson, CancellationToken cancellationToken)
    {
        var normalizedType = feedbackType.Trim();
        var lemma = targetWord.Trim().ToLowerInvariant();

        var feedback = new UserFeedback
        {
            UserId = userId,
            FeedbackType = normalizedType,
            TargetWord = lemma,
            ContextJson = contextJson,
            Status = "Pending"
        };
        db.UserFeedbacks.Add(feedback);

        if (string.Equals(normalizedType, "ExcludeWord", StringComparison.OrdinalIgnoreCase))
        {
            var exists = await db.UserWordExcludes.AnyAsync(item => item.UserId == userId && item.WordLemma == lemma, cancellationToken);
            if (!exists)
            {
                db.UserWordExcludes.Add(new UserWordExclude { UserId = userId, WordLemma = lemma });
            }

            feedback.Status = "Processed";
        }

        await db.SaveChangesAsync(cancellationToken);

        if (string.Equals(normalizedType, "DefinitionWrong", StringComparison.OrdinalIgnoreCase))
        {
            var payload = System.Text.Json.JsonSerializer.Serialize(new { feedbackId = feedback.Id });
            await backgroundJobs.EnqueueAsync(
                "ReAnnotation",
                payload,
                $"reannotation:{feedback.Id}",
                cancellationToken);
        }

        if (string.Equals(normalizedType, "MarkKnown", StringComparison.OrdinalIgnoreCase))
        {
            var word = await db.Words.AsNoTracking().FirstOrDefaultAsync(item => item.Lemma == lemma, cancellationToken);
            if (word is not null)
            {
                var relationship = await users.GetOrCreateRelationshipAsync(userId, word.Id, cancellationToken);
                relationship.EstimatedKnownRate = Math.Min(0.95, relationship.EstimatedKnownRate + 0.2);
                relationship.PersonalUpdatedAt = DateTimeOffset.UtcNow;
                feedback.Status = "Processed";
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
