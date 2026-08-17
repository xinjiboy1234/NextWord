using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-061 难度标注 worker：真实 LLM 结果落库（IntrinsicScore/CEFR/难度档）、
/// Mock 结果跳过不落库（防污染）、重跑幂等（已标注词不再送 LLM）。
/// 共享库纪律：种子词 lemma 唯一、例末清理；不依赖全局词池洁净。
/// </summary>
public class DifficultyAnnotationWorkerTests
{
    private static BackgroundJob NewJob(int? batchSize = null) => new()
    {
        JobType = DifficultyAnnotationWorker.JobType,
        PayloadJson = batchSize is null ? "{}" : $"{{\"batchSize\": {batchSize}}}",
        Status = "Pending"
    };

    private static async Task<Word> SeedWordAsync(ApplicationDbContext db, string salt, CefrLevel cefr = CefrLevel.B1)
    {
        var word = new Word
        {
            Lemma = $"diff{salt}-{Guid.NewGuid():N}"[..24],
            PartOfSpeech = "v",
            Meanings = ["测试词义"],
            DifficultyLevel = DifficultyLevel.Intermediate,
            CefrLevel = cefr
        };
        db.Words.Add(word);
        await db.SaveChangesAsync();
        return word;
    }

    /// <summary>固定返回真实难度标注的 LLM 桩（ModelProfileId=难度标注 profile）。</summary>
    private sealed class RealRatingLlm(Func<ItemRatingRequest, DifficultyRating> respond) : ILLMProvider
    {
        public List<string> RequestedLemmas { get; } = [];
        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
        {
            RequestedLemmas.Add(request.Text);
            return Task.FromResult(respond(request));
        }
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    /// <summary>固定返回 Mock 结果（ModelProfileId=local-dev）的桩：worker 应跳过不落库。</summary>
    private sealed class MockRatingLlm : ILLMProvider
    {
        public int Calls { get; private set; }
        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new DifficultyRating(ItemType.Word, DifficultyLevel.Basic, CefrLevel.A1, "Mock rating.", RecommendedAction.LearnNow, 0.86, "local-dev"));
        }
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<BottleneckInsightResponse> GenerateBottleneckInsightAsync(BottleneckInsightRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Worker_writes_real_rating_with_intrinsic_score()
    {
        await using var db = await PostgresTestDatabase.CreateIsolatedContextAsync();
        var word = await SeedWordAsync(db, "real");
        var llm = new RealRatingLlm(request => new DifficultyRating(
            ItemType.Word, DifficultyLevel.Intermediate, CefrLevel.B1,
            "Common B1 word.", RecommendedAction.ReviewLater, 0.9, DifficultyAnnotationWorker.ModelProfileId, 52));
        var worker = new DifficultyAnnotationWorker(db, llm, NullLogger<DifficultyAnnotationWorker>.Instance);

        await worker.ProcessAsync(NewJob(), CancellationToken.None);

        var stored = await db.Words.Include(item => item.LlmAnnotation).SingleAsync(item => item.Id == word.Id);
        Assert.NotNull(stored.LlmAnnotation);
        Assert.Equal(52, stored.LlmAnnotation.IntrinsicScore);
        Assert.Equal(CefrLevel.B1, stored.LlmAnnotation.CefrLevel);
        Assert.Equal(CefrLevel.B1, stored.CefrLevel);
        Assert.Equal(DifficultyLevel.Intermediate, stored.DifficultyLevel);
        Assert.Equal(DifficultyAnnotationWorker.ModelProfileId, stored.LlmAnnotation.ModelProfileId);
        Assert.True(stored.LlmAnnotation.IsCurrent);

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Worker_skips_mock_ratings_and_persists_nothing()
    {
        await using var db = await PostgresTestDatabase.CreateIsolatedContextAsync();
        var word = await SeedWordAsync(db, "mock");
        var llm = new MockRatingLlm();
        var worker = new DifficultyAnnotationWorker(db, llm, NullLogger<DifficultyAnnotationWorker>.Instance);

        await worker.ProcessAsync(NewJob(), CancellationToken.None);

        Assert.True(llm.Calls >= 1, "Mock 环境下 worker 会尝试标注但结果不落库");
        var stored = await db.Words.Include(item => item.LlmAnnotation).SingleAsync(item => item.Id == word.Id);
        Assert.Null(stored.LlmAnnotation);
        Assert.Equal(0, await db.WordDifficultyAnnotations.CountAsync(item => item.WordId == word.Id));

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Worker_rerun_is_idempotent_and_skips_annotated_words()
    {
        await using var db = await PostgresTestDatabase.CreateIsolatedContextAsync();
        var word = await SeedWordAsync(db, "idem");
        var llm = new RealRatingLlm(request => new DifficultyRating(
            ItemType.Word, DifficultyLevel.Intermediate, CefrLevel.B1,
            "Common B1 word.", RecommendedAction.ReviewLater, 0.9, DifficultyAnnotationWorker.ModelProfileId, 52));
        var worker = new DifficultyAnnotationWorker(db, llm, NullLogger<DifficultyAnnotationWorker>.Instance);

        await worker.ProcessAsync(NewJob(), CancellationToken.None);
        Assert.Single(llm.RequestedLemmas);

        // 重跑：已标注词不再送 LLM（批次只捞 IntrinsicScore 为空的词）
        await worker.ProcessAsync(NewJob(), CancellationToken.None);
        Assert.Equal(1, llm.RequestedLemmas.Count);
        Assert.Equal(1, await db.WordDifficultyAnnotations.CountAsync(item => item.WordId == word.Id));

        await db.Database.EnsureDeletedAsync();
    }
}
