using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// ScenarioAnnotationWorker：幂等可重跑、断点可续（设计方案 §6 验收 4）。连真实 PG。
/// </summary>
public sealed class ScenarioAnnotationWorkerTests
{
    [Fact]
    public async Task Worker_AnnotatesUnannotatedWords_AndSkipsThemOnRerun()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var stamp = Guid.NewGuid().ToString("N")[..10];
        var pendingLemma = $"zz{stamp}a";
        var doneLemma = $"zz{stamp}b";

        db.Words.Add(new Word { Lemma = pendingLemma, PartOfSpeech = "v.", Meanings = ["测试词"] });
        db.Words.Add(new Word
        {
            Lemma = doneLemma,
            PartOfSpeech = "n.",
            Meanings = ["已标注"],
            Utility = WordUtility.High,
            Role = ExpressionRole.SceneNoun,
            ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion,
            Scenarios = [new WordScenario { ScenarioKey = "shopping" }]
        });
        await db.SaveChangesAsync();

        var llm = new StubScenarioLlm(_ => new ScenarioAnnotationResult(
            pendingLemma, ["daily_routine", "home_cooking"], WordUtility.High, ExpressionRole.CoreVerb));
        var worker = new ScenarioAnnotationWorker(db, llm, NullLogger<ScenarioAnnotationWorker>.Instance);

        await worker.ProcessAsync(NewJob(), CancellationToken.None);

        var pending = await db.Words.Include(word => word.Scenarios).SingleAsync(word => word.Lemma == pendingLemma);
        Assert.Equal(ScenarioAnnotationWorker.CurrentVersion, pending.ScenarioAnnotationVersion);
        Assert.Equal(WordUtility.High, pending.Utility);
        Assert.Equal(ExpressionRole.CoreVerb, pending.Role);
        Assert.Equal(["daily_routine", "home_cooking"], pending.Scenarios.Select(item => item.ScenarioKey).Order().ToList());

        // 重跑幂等：已标注词不再送给 LLM，既有标注不被覆盖
        llm.Reset();
        await worker.ProcessAsync(NewJob(), CancellationToken.None);

        Assert.DoesNotContain(llm.RequestedLemmas, lemma => lemma is not null && lemma.StartsWith($"zz{stamp}"));
        var untouched = await db.Words.Include(word => word.Scenarios).SingleAsync(word => word.Lemma == doneLemma);
        Assert.Equal(["shopping"], untouched.Scenarios.Select(item => item.ScenarioKey).ToList());

        // 清理，避免污染共享测试库的其他用例
        db.Words.Remove(pending);
        db.Words.Remove(untouched);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Worker_LeavesWordsMissingFromLlmResponse_ForNextRun()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var stamp = Guid.NewGuid().ToString("N")[..10];
        var lemma = $"zz{stamp}c";
        db.Words.Add(new Word { Lemma = lemma, PartOfSpeech = "n.", Meanings = ["漏标词"] });
        await db.SaveChangesAsync();

        // LLM 漏返回该词 → 不得标记版本、不得死循环，留给下次重跑续标
        var llm = new StubScenarioLlm(_ => null);
        var worker = new ScenarioAnnotationWorker(db, llm, NullLogger<ScenarioAnnotationWorker>.Instance);

        await worker.ProcessAsync(NewJob(), CancellationToken.None);

        var word = await db.Words.SingleAsync(item => item.Lemma == lemma);
        Assert.Equal(0, word.ScenarioAnnotationVersion);
        Assert.Null(word.Utility);

        db.Words.Remove(word);
        await db.SaveChangesAsync();
    }

    private static BackgroundJob NewJob() => new()
    {
        JobType = ScenarioAnnotationWorker.JobType,
        PayloadJson = "{}",
        IdempotencyKey = $"test:{Guid.NewGuid():N}",
        Status = "Processing"
    };

    private sealed class StubScenarioLlm(Func<string, ScenarioAnnotationResult?> annotate) : ILLMProvider
    {
        public List<string?> RequestedLemmas { get; } = [];

        public void Reset() => RequestedLemmas.Clear();

        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken)
        {
            var results = new List<ScenarioAnnotationResult>();
            foreach (var item in request.Words)
            {
                RequestedLemmas.Add(item.Lemma);
                var result = annotate(item.Lemma);
                if (result is not null)
                {
                    results.Add(result);
                }
            }

            return Task.FromResult(new ScenarioAnnotationResponse(results));
        }

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
