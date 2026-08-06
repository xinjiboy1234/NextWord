using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ScoreProfileService(
    ApplicationDbContext db,
    IScoreMappingService mapping) : IScoreProfileService
{
    /// <summary>T-038：cefrDisplay 降档迟滞天数——Overall 连续 3 天低于当前展示档下限才降档。</summary>
    private const int CefrDisplayHysteresisDays = 3;
    public async Task<UserProfileScores> GetScoresAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await GetOrCreateProgressAsync(userId, cancellationToken);
        return mapping.Project(progress);
    }

    public async Task<ProfileUpdateResult> ApplyUpdateAsync(ProfileUpdateCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException("IdempotencyKey is required.", nameof(command));
        }

        if (command.Absolute is null && command.Delta is null)
        {
            throw new ArgumentException("Either Absolute or Delta must be provided.", nameof(command));
        }

        var existingEvent = await db.LearningEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
        if (existingEvent is not null)
        {
            var progress = await GetOrCreateProgressAsync(command.UserId, cancellationToken);
            return new ProfileUpdateResult(mapping.Project(progress), Applied: false, SkipReason: "duplicate");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var progress = await GetOrCreateProgressAsync(command.UserId, cancellationToken);
            // T-038：迟滞判定需要「更新前」的展示档作为当前档
            var previousCefrDisplay = progress.CefrDisplay;

            if (command.Absolute is not null)
            {
                ApplyAbsolute(progress, command.Absolute);
            }
            else if (command.Delta is not null)
            {
                ApplyDelta(progress, command.Delta);
            }

            SyncLegacyLevels(progress);
            // T-038：cefrDisplay 下行迟滞——上行即时、降档需连续低于下限；测评定级写入（权威锚点）不受约束
            if (!command.BypassCefrDisplayHysteresis)
            {
                progress.CefrDisplay = await ApplyCefrDisplayHysteresisAsync(
                    command.UserId,
                    previousCefrDisplay,
                    progress.CefrDisplay,
                    mapping.ComputeOverall(progress.VocabularyScore, progress.ReadingScore, progress.WritingScore),
                    cancellationToken);
            }

            progress.ScoresUpdatedAt = DateTimeOffset.UtcNow;
            progress.ScoreSchemaVersion = 1;

            db.LearningEvents.Add(new LearningEvent
            {
                UserId = command.UserId,
                EventType = command.Source,
                PayloadJson = command.PayloadJson ?? "{}",
                OccurredAt = DateTimeOffset.UtcNow,
                IdempotencyKey = command.IdempotencyKey
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ProfileUpdateResult(mapping.Project(progress), Applied: true);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void ApplyAbsolute(UserProgress progress, ProfileScoreAssignment absolute)
    {
        if (absolute.Vocabulary is int vocabulary)
        {
            progress.VocabularyScore = mapping.ClampScore(vocabulary);
        }

        if (absolute.Reading is int reading)
        {
            progress.ReadingScore = mapping.ClampScore(reading);
        }

        if (absolute.Writing is int writing)
        {
            progress.WritingScore = mapping.ClampScore(writing);
        }

        if (absolute.Spelling is int spelling)
        {
            progress.SpellingScore = mapping.ClampScore(spelling);
        }
    }

    private void ApplyDelta(UserProgress progress, ProfileScoreDelta delta)
    {
        if (delta.Vocabulary is int vocabularyDelta)
        {
            progress.VocabularyScore = mapping.ClampScore((progress.VocabularyScore ?? 0) + vocabularyDelta);
        }

        if (delta.Reading is int readingDelta)
        {
            progress.ReadingScore = mapping.ClampScore((progress.ReadingScore ?? 0) + readingDelta);
        }

        if (delta.Writing is int writingDelta)
        {
            progress.WritingScore = mapping.ClampScore((progress.WritingScore ?? 0) + writingDelta);
        }

        if (delta.Spelling is int spellingDelta)
        {
            progress.SpellingScore = mapping.ClampScore((progress.SpellingScore ?? 0) + spellingDelta);
        }
    }

    private void SyncLegacyLevels(UserProgress progress)
    {
        if (progress.VocabularyScore is int vocabulary)
        {
            progress.VocabLevel = mapping.MapScoreToCefrLevel(vocabulary);
        }

        if (progress.ReadingScore is int reading)
        {
            progress.ReadingLevel = mapping.MapScoreToCefrLevel(reading);
        }

        if (progress.WritingScore is int writing)
        {
            progress.SentenceLevel = mapping.MapScoreToCefrLevel(writing);
        }

        if (progress.SpellingScore is int spelling)
        {
            progress.SpellingLevel = mapping.MapScoreToCefrLevel(spelling);
        }

        var overall = mapping.ComputeOverall(progress.VocabularyScore, progress.ReadingScore, progress.WritingScore);
        progress.OverallLevel = mapping.MapScoreToCefrLevel(overall);
        progress.CefrDisplay = mapping.MapToCefr(overall);
        progress.DifficultyBucket = mapping.MapToBucket(overall);
    }

    /// <summary>
    /// T-038 cefrDisplay 下行迟滞（只影响展示档，OverallLevel 升级规则与分数本身不动）：
    /// 上行即时——Overall 升过当前展示档上限（raw 档高于当前展示档）立即升档；
    /// 降档需当前 Overall 与近 3 天 ProfileScoreSnapshots 的 Overall 全部低于当前展示档下限，快照不足 3 天不降。
    /// </summary>
    private async Task<string?> ApplyCefrDisplayHysteresisAsync(
        Guid userId,
        string? previousDisplay,
        string? rawDisplay,
        int overall,
        CancellationToken cancellationToken)
    {
        if (previousDisplay is null || rawDisplay is null || rawDisplay == previousDisplay)
        {
            return rawDisplay;
        }

        if (!Enum.TryParse<CefrLevel>(previousDisplay, out var previousLevel)
            || !Enum.TryParse<CefrLevel>(rawDisplay, out var rawLevel))
        {
            return rawDisplay;
        }

        if (rawLevel > previousLevel)
        {
            return rawDisplay; // 上行即时：涨档是鼓励
        }

        var previousBand = mapping.GetCefrBand(previousDisplay);
        if (previousBand is null || overall >= previousBand.Min)
        {
            return rawDisplay; // 未跌破当前展示档下限，照常映射
        }

        var recentOveralls = await db.ProfileScoreSnapshots
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.Date)
            .Take(CefrDisplayHysteresisDays)
            .Select(item => item.ScoresJson)
            .ToListAsync(cancellationToken);

        if (recentOveralls.Count < CefrDisplayHysteresisDays)
        {
            return previousDisplay; // 快照不足 3 天不降
        }

        var allBelow = recentOveralls.All(json => ReadSnapshotOverall(json) is int snapshotOverall && snapshotOverall < previousBand.Min);
        return allBelow ? rawDisplay : previousDisplay;
    }

    /// <summary>从快照 ScoresJson（UserProfileScores 序列化）读 Overall；读不到返回 null（视为不满足「低于下限」）。</summary>
    private static int? ReadSnapshotOverall(string scoresJson)
    {
        try
        {
            using var document = JsonDocument.Parse(scoresJson);
            return document.RootElement.TryGetProperty("overall", out var overall) && overall.TryGetInt32(out var value)
                ? value
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<UserProgress> GetOrCreateProgressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await db.UserProgress.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (progress is not null)
        {
            return progress;
        }

        progress = new UserProgress { UserId = userId };
        db.UserProgress.Add(progress);
        await db.SaveChangesAsync(cancellationToken);
        return progress;
    }
}
