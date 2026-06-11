using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class LearningEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/learning").WithTags("Learning");

        group.MapPost("/submit", async (
            SubmitLearningRequest request,
            IUserRepository users,
            IWordRepository words,
            ISm2Service sm2,
            CancellationToken ct) =>
        {
            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            var word = await words.GetByIdAsync(request.WordId, ct);
            if (word is null)
            {
                return Results.NotFound(new { message = "Word not found." });
            }

            var isCorrect = IsAnswerCorrect(request.Answer, word.Meanings);
            var relationship = await users.GetOrCreateRelationshipAsync(user.Id, word.Id, ct);
            relationship.TimesLearned += 1;
            relationship.TimesCorrect += isCorrect ? 1 : 0;
            relationship.MasteryScore = Math.Clamp(relationship.MasteryScore + ScoreDelta(request.Rating, isCorrect), 0, 100);
            sm2.ApplyReview(relationship, request.Rating, DateTimeOffset.UtcNow);

            var log = new WordLearningLog
            {
                UserId = user.Id,
                WordId = word.Id,
                Answer = request.Answer.Trim(),
                IsCorrect = isCorrect,
                Rating = request.Rating,
                ResponseTimeMs = request.ResponseTimeMs
            };
            await users.AddLearningLogAsync(log, ct);

            var progress = await users.GetOrCreateProgressAsync(user.Id, ct);
            var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.LocalDateTime);
            if (progress.LastStudyDate != today)
            {
                progress.StreakDays = progress.LastStudyDate == today.AddDays(-1) ? progress.StreakDays + 1 : 1;
                progress.LastStudyDate = today;
            }

            await users.SaveChangesAsync(ct);
            return Results.Ok(new LearningResultDto(
                isCorrect,
                word.Meanings,
                word.ExampleSentences,
                relationship.MasteryScore,
                relationship.NextReviewDue,
                relationship.IntervalDays));
        });
    }

    private static bool IsAnswerCorrect(string answer, IEnumerable<string> meanings)
    {
        var normalizedAnswer = Normalize(answer);
        return meanings.Any(meaning => Normalize(meaning).Contains(normalizedAnswer) || normalizedAnswer.Contains(Normalize(meaning)));
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("，", ",").Replace("。", ".");
    }

    private static double ScoreDelta(AssessmentResult rating, bool isCorrect)
    {
        return rating switch
        {
            AssessmentResult.Remembered when isCorrect => 18,
            AssessmentResult.Fuzzy => 8,
            AssessmentResult.Forgot => -12,
            _ when isCorrect => 5,
            _ => -5
        };
    }
}

public sealed record SubmitLearningRequest(
    Guid? UserId,
    Guid WordId,
    string Answer,
    AssessmentResult Rating,
    int ResponseTimeMs);

public sealed record LearningResultDto(
    bool IsCorrect,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    double MasteryScore,
    DateTimeOffset NextReviewDue,
    int IntervalDays);
