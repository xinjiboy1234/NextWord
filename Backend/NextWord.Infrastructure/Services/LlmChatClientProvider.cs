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

    public async Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for contextual word definitions."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildDefinitionPrompt(request))
                ],
                new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 800 },
                cancellationToken);
            return LlmResponseParser.ParseDefinition(response.Text, request.Word, request.Context);
        }
        catch
        {
            // T-049：真实 LLM 失败静默回退 Mock；Mock 释义自带 IsFallback 标记（不缓存、降级可见）
            return await fallback.GetDefinitionAsync(request, cancellationToken);
        }
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

    public async Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for vocabulary extraction."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildVocabExtractPrompt(request))
                ],
                new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 2000 },
                cancellationToken);
            return LlmResponseParser.ParseVocabExtract(response.Text);
        }
        catch
        {
            return await fallback.ExtractVocabAsync(request, cancellationToken);
        }
    }

    public async Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You are a helpful English reading tutor."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildCommentReplyPrompt(request))
                ],
                new ChatOptions { Temperature = 0.3f, MaxOutputTokens = 400 },
                cancellationToken);
            return new CommentReplyResponse(response.Text.Trim());
        }
        catch
        {
            return await fallback.ReplyToCommentAsync(request, cancellationToken);
        }
    }

    public async Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for vocabulary scenario annotation."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildScenarioAnnotationPrompt(request))
                ],
                new ChatOptions { Temperature = 0.1f, MaxOutputTokens = 4000 },
                cancellationToken);
            return LlmResponseParser.ParseScenarioAnnotation(response.Text);
        }
        catch
        {
            return await fallback.AnnotateScenarioAsync(request, cancellationToken);
        }
    }

    public async Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for an English learner weakness profile."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildWeaknessProfilePrompt(request))
                ],
                new ChatOptions { Temperature = 0.2f, MaxOutputTokens = 3000 },
                cancellationToken);
            return LlmResponseParser.ParseWeaknessProfile(response.Text);
        }
        catch
        {
            return await fallback.GenerateWeaknessProfileAsync(request, cancellationToken);
        }
    }

    public async Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await chatClient.GetResponseAsync(
                [
                    new ChatMessage(ChatRole.System, "You return compact, valid JSON for an English learner bottleneck insight."),
                    new ChatMessage(ChatRole.User, LlmPromptFactory.BuildBottleneckInsightPrompt(request))
                ],
                new ChatOptions { Temperature = 0.2f, MaxOutputTokens = 1000 },
                cancellationToken);
            return LlmResponseParser.ParseBottleneckInsight(response.Text);
        }
        catch
        {
            return await fallback.GenerateBottleneckInsightAsync(request, cancellationToken);
        }
    }
}
