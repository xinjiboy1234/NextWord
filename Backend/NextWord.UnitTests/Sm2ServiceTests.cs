using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Services;

namespace NextWord.UnitTests;

public class Sm2ServiceTests
{
    private readonly Sm2Service _service = new();

    [Fact]
    public void Remembered_increases_interval_and_repeat_count()
    {
        var relationship = new UserWordRelationship { RepeatCount = 1, IntervalDays = 6, EaseFactor = 2.5 };
        var reviewedAt = DateTimeOffset.UtcNow;

        _service.ApplyReview(relationship, AssessmentResult.Remembered, reviewedAt);

        Assert.Equal(2, relationship.RepeatCount);
        Assert.True(relationship.IntervalDays >= 6);
        Assert.True(relationship.NextReviewDue > reviewedAt);
    }

    [Fact]
    public void Forgot_resets_repeat_count_and_lowers_ease()
    {
        var relationship = new UserWordRelationship { RepeatCount = 3, IntervalDays = 20, EaseFactor = 2.5 };

        _service.ApplyReview(relationship, AssessmentResult.Forgot, DateTimeOffset.UtcNow);

        Assert.Equal(0, relationship.RepeatCount);
        Assert.Equal(1, relationship.IntervalDays);
        Assert.True(relationship.EaseFactor < 2.5);
    }
}
