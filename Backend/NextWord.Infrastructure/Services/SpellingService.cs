using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Infrastructure.Data;

namespace NextWord.Infrastructure.Services;

public sealed class SpellingService(ApplicationDbContext db, ISm2Service sm2) : ISpellingService
{
    public async Task<SpellingLog> SubmitAsync(Guid userId, Guid wordId, string userSpelling, int attempts, CancellationToken cancellationToken)
    {
        var word = await db.Words.FirstOrDefaultAsync(item => item.Id == wordId, cancellationToken)
            ?? throw new InvalidOperationException("Word not found.");

        var normalizedInput = Normalize(userSpelling);
        var normalizedCorrect = Normalize(word.Lemma);
        var isCorrect = string.Equals(normalizedInput, normalizedCorrect, StringComparison.OrdinalIgnoreCase);
        var log = new SpellingLog
        {
            UserId = userId,
            WordId = word.Id,
            UserSpelling = userSpelling.Trim(),
            CorrectSpelling = word.Lemma,
            IsCorrect = isCorrect,
            ErrorPositions = FindErrorPositions(normalizedInput, normalizedCorrect),
            Attempts = Math.Max(1, attempts)
        };

        var relationship = await db.UserWordRelationships
            .FirstOrDefaultAsync(item => item.UserId == userId && item.WordId == word.Id, cancellationToken);
        if (relationship is null)
        {
            relationship = new UserWordRelationship
            {
                UserId = userId,
                WordId = word.Id,
                Source = WordSource.New
            };
            db.UserWordRelationships.Add(relationship);
        }

        relationship.TimesLearned += 1;
        relationship.TimesCorrect += isCorrect ? 1 : 0;
        relationship.MasteryScore = Math.Clamp(relationship.MasteryScore + (isCorrect ? 10 : -10), 0, 100);
        sm2.ApplyReview(relationship, isCorrect ? AssessmentResult.Remembered : AssessmentResult.Forgot, DateTimeOffset.UtcNow);

        if (!isCorrect && await HasPreviousSpellingMissAsync(userId, word.Id, cancellationToken))
        {
            relationship.NextReviewDue = DateTimeOffset.UtcNow;
            relationship.EaseFactor = Math.Max(1.3, relationship.EaseFactor - 0.15);
        }

        db.SpellingLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static List<int> FindErrorPositions(string input, string correct)
    {
        var positions = new List<int>();
        var max = Math.Max(input.Length, correct.Length);
        for (var index = 0; index < max; index += 1)
        {
            var inputChar = index < input.Length ? input[index] : '\0';
            var correctChar = index < correct.Length ? correct[index] : '\0';
            if (inputChar != correctChar)
            {
                positions.Add(index);
            }
        }

        return positions;
    }

    private async Task<bool> HasPreviousSpellingMissAsync(Guid userId, Guid wordId, CancellationToken cancellationToken)
    {
        return await db.SpellingLogs
            .Where(log => log.UserId == userId && log.WordId == wordId)
            .OrderByDescending(log => log.Timestamp)
            .Take(1)
            .AnyAsync(log => !log.IsCorrect, cancellationToken);
    }
}
