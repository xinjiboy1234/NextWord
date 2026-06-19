using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ChallengeService(
    ApplicationDbContext db,
    IChallengePackGenerator packGenerator,
    ILevelEngine levelEngine,
    IUserRepository users) : IChallengeService
{
    public async Task<ChallengePack> StartChallengeAsync(Guid userId, bool confirmationChallenge, CancellationToken cancellationToken)
    {
        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        if (confirmationChallenge)
        {
            progress.IsLevelLocked = true;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await packGenerator.GenerateAsync(userId, progress.OverallLevel, confirmationChallenge, cancellationToken);
    }

    public async Task<ChallengeRecord> SubmitChallengeAsync(
        Guid userId,
        ChallengeType type,
        double vocabScore,
        double sentenceScore,
        double readingScore,
        bool confirmationChallenge,
        CancellationToken cancellationToken)
    {
        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        var passed = vocabScore >= 60 && sentenceScore >= 3.5 && readingScore >= 100;
        var total = Math.Round((vocabScore + sentenceScore * 20 + readingScore) / 3, 1);

        var record = new ChallengeRecord
        {
            UserId = userId,
            ChallengeType = type,
            VocabularyScore = vocabScore,
            SentenceScore = sentenceScore,
            ReadingScore = readingScore,
            TotalScore = total,
            Passed = passed,
            AttemptedLevel = confirmationChallenge ? levelEngine.GetNextLevel(progress.OverallLevel) : progress.OverallLevel
        };
        db.ChallengeRecords.Add(record);

        if (confirmationChallenge)
        {
            progress.IsLevelLocked = false;
            if (passed)
            {
                var from = progress.OverallLevel;
                var to = levelEngine.GetNextLevel(from);
                progress.OverallLevel = to;
                progress.VocabLevel = to;
                progress.ReadingLevel = to;
                progress.SentenceLevel = to;
                progress.LevelStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
                db.LevelHistories.Add(new LevelHistory
                {
                    UserId = userId,
                    FromLevel = from,
                    ToLevel = to,
                    Reason = LevelChangeReason.Upgrade
                });
            }
            else
            {
                db.LevelHistories.Add(new LevelHistory
                {
                    UserId = userId,
                    FromLevel = progress.OverallLevel,
                    ToLevel = progress.OverallLevel,
                    Reason = LevelChangeReason.Rollback
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return record;
    }

    public async Task<IReadOnlyList<ChallengeRecord>> GetRecentAsync(Guid userId, int count, CancellationToken cancellationToken)
    {
        return await db.ChallengeRecords.AsNoTracking()
            .Where(record => record.UserId == userId)
            .OrderByDescending(record => record.Timestamp)
            .Take(Math.Clamp(count, 1, 30))
            .ToListAsync(cancellationToken);
    }
}

public sealed class LevelDashboardService(ApplicationDbContext db, ILevelEngine levelEngine)
{
    public async Task<LevelDashboardDto> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await db.UserProgress.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (progress is null)
        {
            return new LevelDashboardDto(CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, CefrLevel.A1, false, false, []);
        }

        var histories = await db.LevelHistories.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Timestamp)
            .Take(10)
            .ToListAsync(cancellationToken);

        var recentChallenges = await db.ChallengeRecords.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Timestamp)
            .Take(5)
            .ToListAsync(cancellationToken);

        var upgrade = levelEngine.EvaluateUpgradeCandidate(progress, recentChallenges);
        return new LevelDashboardDto(
            progress.OverallLevel,
            progress.VocabLevel,
            progress.SpellingLevel,
            progress.SentenceLevel,
            progress.ReadingLevel,
            progress.HasCompletedInitialAssessment,
            upgrade.IsCandidate,
            histories);
    }

    public async Task<IReadOnlyList<LevelHistory>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.LevelHistories.AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Timestamp)
            .ToListAsync(cancellationToken);
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
    IReadOnlyList<LevelHistory> RecentHistory);
