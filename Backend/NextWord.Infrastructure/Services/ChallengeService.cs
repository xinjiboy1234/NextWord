using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ChallengeService(
    ApplicationDbContext db,
    IChallengePackGenerator packGenerator,
    ILevelEngine levelEngine,
    IUserRepository users,
    IScoreProfileService scoreProfile,
    IAssessmentScoringService scoring,
    ISentenceService sentenceService,
    IEvaluationReportService evaluationReports,
    IOptions<ChallengeThresholdsOptions> thresholds) : IChallengeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ChallengeStartResponse> StartChallengeAsync(Guid userId, bool confirmationChallenge, CancellationToken cancellationToken)
    {
        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        if (confirmationChallenge)
        {
            progress.IsLevelLocked = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        var pack = await packGenerator.GenerateAsync(userId, progress.OverallLevel, confirmationChallenge, cancellationToken);
        var session = new ChallengeSession
        {
            UserId = userId,
            PackJson = JsonSerializer.Serialize(pack, JsonOptions),
            ConfirmationChallenge = confirmationChallenge,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(2)
        };
        db.ChallengeSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var clientView = new ChallengePackClientView(
            pack.Vocabulary.Select(q => new VocabQuizQuestionClient(q.Word, q.Options, q.Difficulty.ToString())).ToList(),
            pack.Sentence,
            GetReadings(pack).Select(q => new ReadingQuizQuestionClient(q.ArticleId, q.Question, q.Options, q.ArticleExcerpt)).ToList(),
            pack.AttemptedLevel.ToString());

        return new ChallengeStartResponse(session.Id, clientView);
    }

    /// <summary>T-035：阅读题组取值——新会话用 Readings，旧会话 JSON 无该属性回退单题 Reading。</summary>
    private static IReadOnlyList<ReadingQuizQuestion> GetReadings(ChallengePack pack) =>
        pack.Readings is { Count: > 0 } readings ? readings : [pack.Reading];

    public async Task<ChallengeSubmitResponse> SubmitChallengeAsync(Guid userId, ChallengeSubmitRequest request, CancellationToken cancellationToken)
    {
        var session = await db.ChallengeSessions.FirstOrDefaultAsync(item => item.Id == request.ChallengeSessionId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Challenge session not found.");

        if (session.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("Challenge session expired.");
        }

        var pack = JsonSerializer.Deserialize<ChallengePack>(session.PackJson, JsonOptions)
            ?? throw new InvalidOperationException("Invalid challenge pack.");

        var vocabCorrect = pack.Vocabulary.Select((q, i) => i < request.VocabAnswers.Count && request.VocabAnswers[i] == q.CorrectIndex).Count(x => x);
        var vocabAccuracy = pack.Vocabulary.Count == 0 ? 0 : (double)vocabCorrect / pack.Vocabulary.Count * 100;
        var vocabScore = scoring.MapVocabToScore(vocabAccuracy);

        var sentenceLog = await sentenceService.RateAsync(
            userId,
            request.SentenceWordId,
            request.TargetWord,
            request.SentenceAnswer,
            request.Scene,
            pack.AttemptedLevel.ToString(),
            cancellationToken);
        var sentenceAverage = (sentenceLog.GrammarScore + sentenceLog.NaturalScore + sentenceLog.VocabularyScore + sentenceLog.RelevanceScore) / 4.0;
        var writingScore = scoring.MapSentenceToScore(sentenceAverage);

        // T-035：阅读按题组正确率映射（3 题 → 0/33/67/100），lookupCount 扣分口径不变；兼容旧单题会话与旧客户端
        var readings = GetReadings(pack);
        IReadOnlyList<int> readingAnswers = request.ReadingSelectedIndexes
            ?? (request.ReadingSelectedIndex is int legacyIndex ? [legacyIndex] : []);
        var readingCorrect = readings
            .Select((q, i) => i < readingAnswers.Count && readingAnswers[i] == q.CorrectIndex)
            .Count(x => x);
        var readingAccuracy = readings.Count == 0 ? 0 : readingCorrect * 100.0 / readings.Count;
        var readingWordCount = readings.Sum(q => q.ArticleExcerpt.Length / 5);
        var readingScore = scoring.MapReadingToScore(readingAccuracy, request.LookupCount, readingWordCount);

        var options = thresholds.Value;
        var passed = vocabAccuracy / 100.0 >= options.VocabAccuracyMin
            && writingScore >= options.WritingScoreMin
            && readingScore >= options.ReadingScoreMin;

        var total = Math.Round((vocabScore + writingScore + readingScore) / 3.0, 1);
        var record = new ChallengeRecord
        {
            UserId = userId,
            ChallengeType = request.ChallengeType,
            VocabularyScore = vocabScore,
            SentenceScore = sentenceAverage,
            ReadingScore = readingScore,
            TotalScore = total,
            Passed = passed,
            AttemptedLevel = pack.AttemptedLevel
        };
        db.ChallengeRecords.Add(record);

        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        if (session.ConfirmationChallenge)
        {
            progress.IsLevelLocked = false;
            if (passed)
            {
                await scoreProfile.ApplyUpdateAsync(
                    new ProfileUpdateCommand(
                        userId,
                        "ChallengePass",
                        null,
                        new ProfileScoreDelta(options.UpgradeDelta, options.UpgradeDelta, options.UpgradeDelta, null),
                        $"challenge:pass:{session.Id}"),
                    cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        long? reportId = null;
        if (session.ConfirmationChallenge)
        {
            reportId = await evaluationReports.EnqueueForUserAsync(
                userId,
                passed ? "ChallengePass" : "ChallengeFail",
                null,
                cancellationToken);
        }

        db.ChallengeSessions.Remove(session);
        await db.SaveChangesAsync(cancellationToken);

        // T-035：Daily 通过即时反馈——规则点评（零 LLM，不改分数）+ 累计通过计数（ChallengeRecords 派生，不加新表）
        string? feedback = null;
        int? passCount = null;
        if (!session.ConfirmationChallenge)
        {
            passCount = await db.ChallengeRecords.AsNoTracking()
                .CountAsync(item => item.UserId == userId && item.Passed, cancellationToken);
            if (passed)
            {
                var profileScores = await scoreProfile.GetScoresAsync(userId, cancellationToken);
                feedback = ChallengeFeedback.Build(vocabScore, writingScore, readingScore, profileScores);
            }
        }

        return new ChallengeSubmitResponse(passed, total, vocabScore, writingScore, readingScore, reportId, feedback, passCount);
    }

    public async Task<IReadOnlyList<ChallengeRecord>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        return (await db.ChallengeRecords.AsNoTracking()
            .Where(record => record.UserId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(record => record.Timestamp)
            .Take(Math.Clamp(count, 1, 30))
            .ToList();
    }
}

public sealed class LevelDashboardService(ApplicationDbContext db, ILevelEngine levelEngine, IScoreProfileService scoreProfile)
{
    public async Task<LevelDashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await db.UserProgress.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (progress is null)
        {
            return new LevelDashboardDto(CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, false, false, [], null, 0, []);
        }

        var histories = (await db.LevelHistories.AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Timestamp)
            .Take(10)
            .ToList();

        var recentChallenges = (await db.ChallengeRecords.AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Timestamp)
            .Take(5)
            .ToList();

        var upgrade = levelEngine.EvaluateUpgradeCandidate(progress, recentChallenges);
        var scores = await scoreProfile.GetScoresAsync(userId, cancellationToken);

        // T-035：挑战荣誉从 ChallengeRecords 派生（不加新表）——累计通过次数 + 各档首次通过标记
        var passedChallenges = await db.ChallengeRecords.AsNoTracking()
            .Where(item => item.UserId == userId && item.Passed)
            .ToListAsync(cancellationToken);
        var firstPassLevels = passedChallenges
            .GroupBy(item => item.AttemptedLevel)
            .Select(group => (Level: group.Key, First: group.Min(item => item.Timestamp)))
            .OrderBy(item => item.First)
            .Select(item => item.Level)
            .ToList();

        return new LevelDashboardDto(
            progress.OverallLevel,
            progress.VocabLevel,
            progress.SpellingLevel,
            progress.SentenceLevel,
            progress.ReadingLevel,
            progress.HasCompletedInitialAssessment,
            upgrade.IsCandidate,
            histories,
            scores,
            passedChallenges.Count,
            firstPassLevels);
    }

    public async Task<IReadOnlyList<LevelHistory>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return (await db.LevelHistories.AsNoTracking()
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.Timestamp)
            .ToList();
    }
}

public sealed record LevelDashboardDto(
    CefrLevel OverallLevel,
    CefrLevel VocabLevel,
    CefrLevel SpellingLevel,
    CefrLevel SentenceLevel,
    CefrLevel ReadingLevel,
    bool HasCompletedInitialAssessment,
    bool UpgradeCandidate,
    IReadOnlyList<LevelHistory> RecentHistory,
    UserProfileScores? Scores,
    int ChallengePassCount,
    IReadOnlyList<CefrLevel> ChallengeFirstPassLevels);
