using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Repositories;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-052/T-067 拼写队列组装：review/new/mixed 三模式、mixed 新旧 4:6（T-067 由 3:7 提升）、一侧不足另一侧补位、
/// 新词按 [vocabScore, vocabScore+12] 难度带内口径取词、双空返回空队列。
/// 共享库纪律：种子词 ScenarioAnnotationVersion 置为当前版本（不被 ScenarioAnnotationWorker 当未标注词捞走，
/// 参照 ScenarioAnnotationWorkerTests 的污染教训），且每例结束清理用户/关系/词；断言不依赖全局词池洁净
/// （带内新词自身补足名额，只断言比例/归属/排除，不断言具体新词集合）。
/// </summary>
public class SpellingQueueTests
{
    private static async Task<(ApplicationDbContext Db, SpellingService Service)> CreateServiceAsync()
    {
        var db = await PostgresTestDatabase.CreateContextAsync();
        var service = new SpellingService(
            db,
            new Sm2Service(),
            new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())),
            new ReviewQueueService(db));
        return (db, service);
    }

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, int? vocabulary = null)
    {
        var user = new User { DisplayName = $"t052-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        if (vocabulary is not null)
        {
            db.UserProgress.Add(new UserProgress { UserId = user.Id, VocabularyScore = vocabulary });
        }

        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Word> SeedWordAsync(ApplicationDbContext db, DifficultyLevel level, List<Guid> seededWordIds, CefrLevel cefr = CefrLevel.A1)
    {
        var word = new Word
        {
            Lemma = $"t052{Guid.NewGuid():N}"[..16],
            PartOfSpeech = "v",
            Meanings = ["测试词义"],
            DifficultyLevel = level,
            // T-061：带内选词按 CEFR 六档映射内在难度分，种子词显式带 CEFR 才可测带内/带外
            CefrLevel = cefr,
            // 共享库纪律：标记为已标注，避免被 ScenarioAnnotationWorker 批次捞走
            ScenarioAnnotationVersion = ScenarioAnnotationWorker.CurrentVersion,
        };
        db.Words.Add(word);
        await db.SaveChangesAsync();
        seededWordIds.Add(word.Id);
        return word;
    }

    private static async Task<Word> SeedDueReviewAsync(ApplicationDbContext db, Guid userId, List<Guid> seededWordIds)
    {
        var word = await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds);
        db.UserWordRelationships.Add(new UserWordRelationship
        {
            UserId = userId,
            WordId = word.Id,
            NextReviewDue = DateTimeOffset.UtcNow.AddHours(-1),
        });
        await db.SaveChangesAsync();
        return word;
    }

    /// <summary>清理本例种子数据，避免污染共享测试库的其他用例。</summary>
    private static async Task CleanupAsync(ApplicationDbContext db, Guid userId, List<Guid> seededWordIds)
    {
        db.UserWordRelationships.RemoveRange(db.UserWordRelationships.Where(item => item.UserId == userId));
        db.UserProgress.RemoveRange(db.UserProgress.Where(item => item.UserId == userId));
        db.Users.RemoveRange(db.Users.Where(item => item.Id == userId));
        db.Words.RemoveRange(db.Words.Where(item => seededWordIds.Contains(item.Id)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetQueue_review_mode_returns_only_due_reviews()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        var user = await SeedUserAsync(db);
        var seededWordIds = new List<Guid>();
        try
        {
            var due = new List<Word>();
            for (var i = 0; i < 3; i++) due.Add(await SeedDueReviewAsync(db, user.Id, seededWordIds));
            for (var i = 0; i < 2; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds); // 未学新词不应出现

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.Review, CancellationToken.None);

            Assert.Equal(3, queue.Count);
            Assert.All(queue, item => Assert.True(item.IsReview));
            Assert.Equal(due.Select(word => word.Id).OrderBy(id => id), queue.Select(item => item.Word.Id).OrderBy(id => id));
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_new_mode_returns_only_new_words()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        var user = await SeedUserAsync(db);
        var seededWordIds = new List<Guid>();
        try
        {
            var due = await SeedDueReviewAsync(db, user.Id, seededWordIds); // 到期复习词不应出现
            for (var i = 0; i < 3; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds);

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.New, CancellationToken.None);

            Assert.NotEmpty(queue);
            Assert.All(queue, item => Assert.False(item.IsReview));
            Assert.DoesNotContain(queue, item => item.Word.Id == due.Id);
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_mixed_mode_keeps_new_to_review_3_to_7()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        var user = await SeedUserAsync(db);
        var seededWordIds = new List<Guid>();
        try
        {
            for (var i = 0; i < 8; i++) await SeedDueReviewAsync(db, user.Id, seededWordIds);
            for (var i = 0; i < 6; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds);

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.Mixed, CancellationToken.None);

            // count=12 → 新 5（4:6 AwayFromZero 取整，T-067）、复习 7
            Assert.Equal(12, queue.Count);
            Assert.Equal(7, queue.Count(item => item.IsReview));
            Assert.Equal(5, queue.Count(item => !item.IsReview));
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_mixed_backfills_with_new_words_when_reviews_short()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        var user = await SeedUserAsync(db);
        var seededWordIds = new List<Guid>();
        try
        {
            var due = new List<Word>();
            for (var i = 0; i < 2; i++) due.Add(await SeedDueReviewAsync(db, user.Id, seededWordIds));
            for (var i = 0; i < 12; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds);

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.Mixed, CancellationToken.None);

            // 复习只有 2 个，新词补位凑满 12
            Assert.Equal(12, queue.Count);
            Assert.Equal(2, queue.Count(item => item.IsReview));
            Assert.Equal(10, queue.Count(item => !item.IsReview));
            Assert.Equal(due.Select(word => word.Id).OrderBy(id => id),
                queue.Where(item => item.IsReview).Select(item => item.Word.Id).OrderBy(id => id));
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_new_words_stay_within_vocab_band()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        // 无分数 → vocab 默认 A2 中点 27 → 难度带 [27,39]：A2（映射 27）带内，A1（10）/B1（52）/B2（77）带外
        var user = await SeedUserAsync(db);
        var seededWordIds = new List<Guid>();
        try
        {
            var outOfBand = new List<Guid>();
            for (var i = 0; i < 25; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds, CefrLevel.A2);
            outOfBand.Add((await SeedWordAsync(db, DifficultyLevel.Basic, seededWordIds, CefrLevel.A1)).Id);
            outOfBand.Add((await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds, CefrLevel.B1)).Id);
            outOfBand.Add((await SeedWordAsync(db, DifficultyLevel.Advanced, seededWordIds, CefrLevel.B2)).Id);

            var queue = await service.GetQueueAsync(user.Id, 20, SpellingQueueMode.New, CancellationToken.None);

            // 带内 A2 词足够（25 ≥ 20），不触发相邻带扩展，带外词不进队列
            Assert.Equal(20, queue.Count);
            Assert.All(queue, item => Assert.False(item.IsReview));
            Assert.DoesNotContain(queue, item => outOfBand.Contains(item.Word.Id));
            Assert.All(queue, item => Assert.Equal(CefrLevel.A2, item.Word.CefrLevel));
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_band_expansion_serves_adjacent_bands_when_band_empty()
    {
        var (db, service) = await CreateServiceAsync();
        await using var _ = db;
        // T-061：vocab=100 → 难度带 [100,100] 空 → 相邻带扩展 [76,100] 覆盖 B2（77）——有未学词即不空队列
        var user = await SeedUserAsync(db, vocabulary: 100);
        var seededWordIds = new List<Guid>();
        try
        {
            for (var i = 0; i < 5; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds, CefrLevel.B2);

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.Mixed, CancellationToken.None);

            Assert.NotEmpty(queue);
            Assert.All(queue, item => Assert.False(item.IsReview));
        }
        finally
        {
            await CleanupAsync(db, user.Id, seededWordIds);
        }
    }

    [Fact]
    public async Task GetQueue_returns_empty_only_when_all_words_learned()
    {
        // T-061：带内扩展 + 全量兜底后，只有「全部未学词都已学」才返回空队列。
        // 用一次性隔离库避免共享测试库（类间并行）被其他测试并发造词导致断言不确定。
        await using var db = await PostgresTestDatabase.CreateIsolatedContextAsync();
        var service = new SpellingService(
            db,
            new Sm2Service(),
            new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())),
            new ReviewQueueService(db));
        var user = await SeedUserAsync(db, vocabulary: 100);
        var seededWordIds = new List<Guid>();
        try
        {
            for (var i = 0; i < 3; i++) await SeedWordAsync(db, DifficultyLevel.Intermediate, seededWordIds, CefrLevel.B2);
            // 学掉库里所有词，使未学池为空
            var allWordIds = await db.Words.AsNoTracking().Select(word => word.Id).ToListAsync();
            db.UserWordRelationships.AddRange(allWordIds.Select(wordId => new UserWordRelationship
            {
                UserId = user.Id,
                WordId = wordId
            }));
            await db.SaveChangesAsync();

            var queue = await service.GetQueueAsync(user.Id, 12, SpellingQueueMode.Mixed, CancellationToken.None);

            Assert.Empty(queue);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
