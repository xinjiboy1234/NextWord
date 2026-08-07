using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-047：背词考察回写 Vocabulary、阅读完成回写 Reading（真实 PG，DESIGN-vr-score-writeback §2/§4）。
/// Vocabulary：observed = 考察词有效难度分 × 表现系数（对 1.0/错 0.3），delta = clamp(round(diff×0.05), -1, +1)，键 vocab-score:{logId}；
/// Reading：observed = 文章难度分 × 查词修正系数（≤5% → 1.0，每超 5% 减 0.1，下限 0.5），delta = clamp(round(diff×0.1), -2, +2)，键 reading-score:{logId}。
/// </summary>
public class VrScoreWritebackTests
{
    // ── Vocabulary ← 背词考察 ────────────────────────────────

    [Fact]
    public async Task Vocab_correct_hard_word_raises_clamped_to_plus_1()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, vocabulary: 50);
        var word = await SeedWordAsync(db, intrinsic: 80);
        var writeback = CreateWriteback(db);

        // observed = 80；raw delta = round(30 × 0.05) = 2 → clamp 到 +1
        var log = LearningLog(user.Id, word.Id, isCorrect: true);
        var change = await writeback.ApplyVocabularyAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(51, change.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(51, progress.VocabularyScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"vocab-score:{log.Id}"));
        var scoreEvent = await db.LearningEvents.SingleAsync(item => item.IdempotencyKey == $"vocab-score:{log.Id}");
        Assert.Equal("VocabularyPractice", scoreEvent.EventType);
    }

    [Fact]
    public async Task Vocab_wrong_answer_lowers_gently()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, vocabulary: 50);
        var word = await SeedWordAsync(db, intrinsic: 80);
        var writeback = CreateWriteback(db);

        // observed = 80 × 0.3 = 24；raw delta = round(-26 × 0.05) = -1
        var log = LearningLog(user.Id, word.Id, isCorrect: false);
        var change = await writeback.ApplyVocabularyAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(49, change.After);
    }

    [Fact]
    public async Task Vocab_big_gap_below_lowers_clamped_to_minus_1()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, vocabulary: 90);
        var word = await SeedWordAsync(db, intrinsic: 20);
        var writeback = CreateWriteback(db);

        // 答错：observed = 20 × 0.3 = 6；raw delta = round(-84 × 0.05) = -4 → clamp 到 -1
        var log = LearningLog(user.Id, word.Id, isCorrect: false);
        var change = await writeback.ApplyVocabularyAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(90, change.Before);
        Assert.Equal(89, change.After);
    }

    [Fact]
    public async Task Vocab_replay_same_log_does_not_apply_twice()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, vocabulary: 50);
        var word = await SeedWordAsync(db, intrinsic: 80);
        var writeback = CreateWriteback(db);
        var log = LearningLog(user.Id, word.Id, isCorrect: true);

        var first = await writeback.ApplyVocabularyAsync(user.Id, log, CancellationToken.None);
        var second = await writeback.ApplyVocabularyAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(51, first.After);
        Assert.Equal(51, second.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(51, progress.VocabularyScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"vocab-score:{log.Id}"));
    }

    // ── Reading ← 阅读完成 ───────────────────────────────────

    [Fact]
    public async Task Reading_low_lookup_rate_full_credit_clamped_to_plus_2()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, reading: 50);
        var article = await SeedArticleAsync(db, DifficultyLevel.Advanced, wordCount: 200);
        var writeback = CreateWriteback(db);

        // 查词率 10/200 = 5% → 系数 1.0；observed = 75；raw delta = round(25 × 0.1) = 3 → clamp 到 +2
        var log = ReadingLog(user.Id, article.Id, lookupCount: 10);
        var change = await writeback.ApplyReadingAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(52, change.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(52, progress.ReadingScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"reading-score:{log.Id}"));
        var scoreEvent = await db.LearningEvents.SingleAsync(item => item.IdempotencyKey == $"reading-score:{log.Id}");
        Assert.Equal("ReadingPractice", scoreEvent.EventType);
    }

    [Fact]
    public async Task Reading_mid_lookup_rate_discounts_observed()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, reading: 50);
        var article = await SeedArticleAsync(db, DifficultyLevel.Advanced, wordCount: 200);
        var writeback = CreateWriteback(db);

        // 查词率 30/200 = 15% → 系数 0.8；observed = 60；raw delta = round(10 × 0.1) = 1
        var log = ReadingLog(user.Id, article.Id, lookupCount: 30);
        var change = await writeback.ApplyReadingAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(51, change.After);
    }

    [Fact]
    public async Task Reading_high_lookup_rate_hits_coefficient_floor()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, reading: 50);
        var article = await SeedArticleAsync(db, DifficultyLevel.Advanced, wordCount: 200);
        var writeback = CreateWriteback(db);

        // 查词率 60/200 = 30% → 系数下限 0.5；observed = 37.5；raw delta = round(-12.5 × 0.1) = -1
        var log = ReadingLog(user.Id, article.Id, lookupCount: 60);
        var change = await writeback.ApplyReadingAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(50, change.Before);
        Assert.Equal(49, change.After);
    }

    [Fact]
    public async Task Reading_replay_same_log_does_not_apply_twice()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, reading: 50);
        var article = await SeedArticleAsync(db, DifficultyLevel.Advanced, wordCount: 200);
        var writeback = CreateWriteback(db);
        var log = ReadingLog(user.Id, article.Id, lookupCount: 10);

        var first = await writeback.ApplyReadingAsync(user.Id, log, CancellationToken.None);
        var second = await writeback.ApplyReadingAsync(user.Id, log, CancellationToken.None);

        Assert.Equal(52, first.After);
        Assert.Equal(52, second.After);
        var progress = await db.UserProgress.SingleAsync(item => item.UserId == user.Id);
        Assert.Equal(52, progress.ReadingScore);
        Assert.Equal(1, await db.LearningEvents.CountAsync(item => item.IdempotencyKey == $"reading-score:{log.Id}"));
    }

    // ── 查词修正系数档位边界 ─────────────────────────────────

    [Theory]
    [InlineData(0.00, 1.0)]
    [InlineData(0.05, 1.0)]
    [InlineData(0.051, 0.9)]
    [InlineData(0.10, 0.9)]
    [InlineData(0.101, 0.8)]
    [InlineData(0.25, 0.6)]
    [InlineData(0.251, 0.5)]
    [InlineData(0.30, 0.5)]
    [InlineData(0.60, 0.5)]
    public void Lookup_coefficient_steps_down_per_5_percent_with_floor(double lookupRate, double expected)
    {
        Assert.Equal(expected, PracticeScoreWritebackService.LookupCoefficient(lookupRate), precision: 9);
    }

    // ── 装配与播种 ───────────────────────────────────────────

    private static PracticeScoreWritebackService CreateWriteback(ApplicationDbContext db)
        => new(new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions())), new AssessmentScoringService(new ScoreMappingOptions()), db);

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, int vocabulary = 50, int reading = 50)
    {
        var user = new User { DisplayName = $"t047-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        db.UserProgress.Add(new UserProgress
        {
            UserId = user.Id,
            VocabularyScore = vocabulary,
            ReadingScore = reading,
            WritingScore = 50,
            CefrDisplay = "B1"
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Word> SeedWordAsync(ApplicationDbContext db, int intrinsic)
    {
        var word = new Word
        {
            Lemma = $"w{Guid.NewGuid():N}"[..12],
            PartOfSpeech = "v",
            Meanings = ["测试词义"],
            DifficultyLevel = DifficultyLevel.Advanced,
            CefrLevel = CefrLevel.B2,
            LlmAnnotation = new WordDifficultyAnnotation
            {
                DifficultyLevel = DifficultyLevel.Advanced,
                CefrLevel = CefrLevel.B2,
                IntrinsicScore = intrinsic
            }
        };
        db.Words.Add(word);
        await db.SaveChangesAsync();
        return word;
    }

    private static async Task<Article> SeedArticleAsync(ApplicationDbContext db, DifficultyLevel level, int wordCount)
    {
        var article = new Article
        {
            Title = $"a-{Guid.NewGuid():N}"[..14],
            Content = "content",
            DifficultyLevel = level,
            WordCount = wordCount
        };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        return article;
    }

    private static WordLearningLog LearningLog(Guid userId, Guid wordId, bool isCorrect) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        WordId = wordId,
        Answer = "answer",
        IsCorrect = isCorrect,
        Rating = isCorrect ? AssessmentResult.Remembered : AssessmentResult.Forgot
    };

    private static ReadingLog ReadingLog(Guid userId, Guid articleId, int lookupCount) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        ArticleId = articleId,
        LookupCount = lookupCount
    };
}
