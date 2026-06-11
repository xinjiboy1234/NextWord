using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public static class LlmPromptFactory
{
    public static string BuildDifficultyPrompt(ItemRatingRequest request)
    {
        return $"Rate the {request.ItemType} difficulty for: {request.Text}";
    }
}
