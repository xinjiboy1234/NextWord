using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Interfaces;

public interface ISm2Service
{
    UserWordRelationship ApplyReview(UserWordRelationship relationship, AssessmentResult rating, DateTimeOffset reviewedAt);
}
