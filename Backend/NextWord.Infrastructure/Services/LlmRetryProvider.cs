using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// LLM 调用重试装饰器：失败时指数退避重试，最终回退 inner provider。
/// </summary>
public sealed class LlmRetryProvider(ILLMProvider inner, int maxAttempts = 3) : ILLMProvider
{
    public async Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.RateDifficultyAsync(request, token), cancellationToken);

    public async Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.GetDefinitionAsync(request, token), cancellationToken);

    public async Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.RateSentenceAsync(request, token), cancellationToken);

    public async Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.ExtractVocabAsync(request, token), cancellationToken);

    public async Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.ReplyToCommentAsync(request, token), cancellationToken);

    public async Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.AnnotateScenarioAsync(request, token), cancellationToken);

    public async Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.GenerateWeaknessProfileAsync(request, token), cancellationToken);

    public async Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken)
        => await ExecuteAsync(token => inner.GenerateBottleneckInsightAsync(request, token), cancellationToken);

    private async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                last = ex;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
        }

        throw last ?? new InvalidOperationException("LLM call failed.");
    }
}
