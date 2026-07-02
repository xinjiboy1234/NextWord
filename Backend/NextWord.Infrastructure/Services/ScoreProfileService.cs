using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class ScoreProfileService(
    ApplicationDbContext db,
    IScoreMappingService mapping) : IScoreProfileService
{
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

            if (command.Absolute is not null)
            {
                ApplyAbsolute(progress, command.Absolute);
            }
            else if (command.Delta is not null)
            {
                ApplyDelta(progress, command.Delta);
            }

            SyncLegacyLevels(progress);
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
