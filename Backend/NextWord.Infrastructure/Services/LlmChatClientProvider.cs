using Microsoft.Extensions.AI;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;

namespace NextWord.Infrastructure.Services;

public sealed class LlmChatClientProvider(IChatClient chatClient, LlmMockProvider fallback) : ILLMProvider
{
    public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
    {
        return fallback.RateDifficultyAsync(request, cancellationToken);
    }

    public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
    {
        return fallback.GetDefinitionAsync(request, cancellationToken);
    }

    public async Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for English learning assessment."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildSentenceRatingPrompt(request))
                ],
                new ChatOptions
                {
                    Temperature = 0.1f,
                    MaxOutputTokens = 800
                },
                cancellationToken);

            return LlmResponseParser.ParseSentenceRating(response.Text);
        }
        catch
        {
            return await fallback.RateSentenceAsync(request, cancellationToken);
        }
    }
}
