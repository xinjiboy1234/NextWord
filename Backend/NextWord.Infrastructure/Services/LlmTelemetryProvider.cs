using Microsoft.Extensions.Logging;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using System.Diagnostics;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// LLM 调用遥测装饰器：记录耗时与 ModelProfileId，便于生产环境观测。
/// </summary>
public sealed class LlmTelemetryProvider(ILLMProvider inner, ILogger<LlmTelemetryProvider> logger) : ILLMProvider
{
    public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("RateDifficulty", request.Options?.ModelProfileId, token => inner.RateDifficultyAsync(request, token), cancellationToken);

    public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("GetDefinition", request.Options?.ModelProfileId, token => inner.GetDefinitionAsync(request, token), cancellationToken);

    public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("RateSentence", request.Options?.ModelProfileId, token => inner.RateSentenceAsync(request, token), cancellationToken);

    public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("ExtractVocab", request.Options?.ModelProfileId, token => inner.ExtractVocabAsync(request, token), cancellationToken);

    public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("ReplyToComment", request.Options?.ModelProfileId, token => inner.ReplyToCommentAsync(request, token), cancellationToken);

    public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("AnnotateScenario", request.Options?.ModelProfileId, token => inner.AnnotateScenarioAsync(request, token), cancellationToken);

    public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("GenerateWeaknessProfile", request.Options?.ModelProfileId, token => inner.GenerateWeaknessProfileAsync(request, token), cancellationToken);

    public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken)
        => ExecuteAsync("GenerateBottleneckInsight", request.Options?.ModelProfileId, token => inner.GenerateBottleneckInsightAsync(request, token), cancellationToken);

    private async Task<T> ExecuteAsync<T>(
        string operation,
        string? modelProfileId,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var profileId = modelProfileId ?? "local-dev";
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await action(cancellationToken);
            stopwatch.Stop();
            logger.LogInformation(
                "LLM {Operation} completed in {ElapsedMs}ms (ProfileId={ProfileId})",
                operation,
                stopwatch.ElapsedMilliseconds,
                profileId);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogWarning(
                ex,
                "LLM {Operation} failed after {ElapsedMs}ms (ProfileId={ProfileId})",
                operation,
                stopwatch.ElapsedMilliseconds,
                profileId);
            throw;
        }
    }
}
