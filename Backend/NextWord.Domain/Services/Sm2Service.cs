using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Domain.Services;

public sealed class Sm2Service : ISm2Service
{
    private const int MaxIntervalDays = 3650;
    private const double MinEaseFactor = 1.3;

    public UserWordRelationship ApplyReview(UserWordRelationship relationship, AssessmentResult rating, DateTimeOffset reviewedAt)
    {
        var interval = relationship.IntervalDays <= 0 ? 1 : relationship.IntervalDays;
        var easeFactor = relationship.EaseFactor <= 0 ? 2.5 : relationship.EaseFactor;

        switch (rating)
        {
            case AssessmentResult.Forgot:
                relationship.RepeatCount = 0;
                relationship.IntervalDays = 1;
                relationship.EaseFactor = Math.Max(MinEaseFactor, easeFactor - 0.2);
                break;
            case AssessmentResult.Fuzzy:
                relationship.IntervalDays = 1;
                relationship.EaseFactor = easeFactor;
                break;
            case AssessmentResult.Remembered:
                relationship.IntervalDays = relationship.RepeatCount switch
                {
                    0 => 1,
                    1 => 6,
                    _ => Math.Max(1, (int)Math.Round(interval * easeFactor))
                };
                relationship.EaseFactor = easeFactor + 0.15;
                relationship.RepeatCount += 1;
                break;
            default:
                relationship.IntervalDays = 1;
                break;
        }

        relationship.IntervalDays = Math.Min(relationship.IntervalDays, MaxIntervalDays);
        relationship.LastReviewDate = reviewedAt;
        relationship.NextReviewDue = reviewedAt.AddDays(relationship.IntervalDays);
        return relationship;
    }
}
