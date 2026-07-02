using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class SentenceLlmScoringWorker(
    ApplicationDbContext db,
    ISentenceService sentenceService,
    IScoreProfileService scoreProfile)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(job.PayloadJson);
        var root = doc.RootElement;
        var userId = root.GetProperty("userId").GetGuid();
        var assessmentId = root.GetProperty("assessmentId").GetGuid();
        var answers = root.GetProperty("answers").Deserialize<List<SentenceAnswerItem>>(JsonOptions) ?? [];

        if (answers.Count == 0)
        {
            return;
        }

        var ratings = new List<int>();
        foreach (var item in answers)
        {
            var log = await sentenceService.RateAsync(
                userId,
                item.WordId,
                item.TargetWord,
                item.Answer,
                item.Scene,
                "B1",
                cancellationToken);
            ratings.Add((log.GrammarScore + log.NaturalScore + log.VocabularyScore + log.RelevanceScore) * 5);
        }

        var average = ratings.Count == 0 ? 0 : (int)Math.Round(ratings.Average());
        await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                userId,
                "SentenceLlmFinal",
                new ProfileScoreAssignment(null, null, Math.Clamp(average, 0, 100), null),
                null,
                $"sentence-llm:{assessmentId}"),
            cancellationToken);
    }

    private sealed record SentenceAnswerItem(Guid? WordId, string TargetWord, string Scene, string Answer);
}
