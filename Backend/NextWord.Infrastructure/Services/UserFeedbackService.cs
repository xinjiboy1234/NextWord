using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class UserFeedbackService(ApplicationDbContext db) : IUserFeedbackService
{
    public async Task SubmitAsync(Guid userId, string feedbackType, string targetWord, string? contextJson, CancellationToken cancellationToken)
    {
        db.UserFeedbacks.Add(new UserFeedback
        {
            UserId = userId,
            FeedbackType = feedbackType,
            TargetWord = targetWord.Trim().ToLowerInvariant(),
            ContextJson = contextJson,
            Status = "Pending"
        });

        if (string.Equals(feedbackType, "ExcludeWord", StringComparison.OrdinalIgnoreCase))
        {
            var lemma = targetWord.Trim().ToLowerInvariant();
            var exists = await db.UserWordExcludes.AnyAsync(item => item.UserId == userId && item.WordLemma == lemma, cancellationToken);
            if (!exists)
            {
                db.UserWordExcludes.Add(new UserWordExclude { UserId = userId, WordLemma = lemma });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
