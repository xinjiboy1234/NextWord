using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public static class LlmResponseParser
{
    public static DifficultyRating EnsureValid(DifficultyRating rating)
    {
        if (rating.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("LLM confidence must be between 0 and 1.");
        }

        return rating;
    }
}
