using System.Text.Json;
using NextWord.Domain.Interfaces;

namespace NextWord.Infrastructure.Services;

public sealed class EvaluationDataAssembler(ILearningToolRegistry tools)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<EvaluationAssemblyResult> AssembleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var scores = await tools.InvokeAsync("get_profile_scores", default, userId, cancellationToken);
        var recentLearning = await tools.InvokeAsync(
            "get_recent_learning",
            JsonSerializer.SerializeToElement(new { limit = 10 }, JsonOptions),
            userId,
            cancellationToken);
        var challengeHistory = await tools.InvokeAsync(
            "get_challenge_history",
            JsonSerializer.SerializeToElement(new { limit = 5 }, JsonOptions),
            userId,
            cancellationToken);

        IReadOnlyList<object> searchEvidence = [];
        try
        {
            searchEvidence = (await tools.InvokeAsync(
                "search_web",
                JsonSerializer.SerializeToElement(new { query = "English vocabulary learning strategies" }, JsonOptions),
                userId,
                cancellationToken) as IEnumerable<object>)?.ToList() ?? [];
        }
        catch
        {
            // Search is optional evidence; template report still works offline.
        }

        return new EvaluationAssemblyResult(scores, recentLearning, challengeHistory, searchEvidence);
    }
}

public sealed record EvaluationAssemblyResult(
    object ProfileScores,
    object RecentLearning,
    object ChallengeHistory,
    IReadOnlyList<object> SearchEvidence);
