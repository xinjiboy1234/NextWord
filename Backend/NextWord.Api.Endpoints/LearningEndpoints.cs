using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Services;

namespace NextWord.Api.Endpoints;

public static class LearningEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/learning").WithTags("Learning");

        group.MapPost("/submit", async (
            HttpContext http,
            SubmitLearningRequest request,
            IUserRepository users,
            IWordRepository words,
            ISm2Service sm2,
            CancellationToken ct) =>
        {
            var user = await UserResolver.ResolveAsync(http, request.UserId, users, ct);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            var word = await words.GetByIdAsync(request.WordId, ct);
            if (word is null)
            {
                return Results.NotFound(new { message = "Word not found." });
            }

            // T-014：考察模式按阶段（认识=看词知义，回忆=看义想词）；回忆模式答词、认识模式答义
            var mode = WordLifecycleService.ParseQuizMode(request.Mode);
            var isCorrect = mode == WordQuizMode.Recall
                ? IsRecallCorrect(request.Answer, word.Lemma)
                : IsAnswerCorrect(request.Answer, word.Meanings);
            var relationship = await users.GetOrCreateRelationshipAsync(user.Id, word.Id, ct);
            relationship.TimesLearned += 1;
            relationship.TimesCorrect += isCorrect ? 1 : 0;
            ApplyKnownRateEma(relationship, request.Rating, isCorrect);
            sm2.ApplyReview(relationship, request.Rating, DateTimeOffset.UtcNow);
            // T-014（DESIGN-word-lifecycle §3）：自评只改 SM-2 排程参数（interval/repetitions），
            // 不再按自评加减掌握度——掌握度由生命周期阶段派生，阶段推进只认考察结果
            WordLifecycleService.ApplyReview(relationship, mode, isCorrect, DateTimeOffset.UtcNow);

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
                relationship.IntervalDays,
                WordLifecycleService.ToToken(relationship.LifecycleStage),
                WordLifecycleService.QuizModeToken(WordLifecycleService.QuizModeForStage(relationship.LifecycleStage))));
        });
    }

    private static bool IsAnswerCorrect(string answer, IEnumerable<string> meanings)
    {
        var normalizedAnswer = Normalize(answer);
        return meanings.Any(meaning => Normalize(meaning).Contains(normalizedAnswer) || normalizedAnswer.Contains(Normalize(meaning)));
    }

    /// <summary>T-014 回忆模式（看义想词）：答案需正确拼出词本身。</summary>
    private static bool IsRecallCorrect(string answer, string lemma)
    {
        var normalizedAnswer = Normalize(answer);
        return normalizedAnswer.Length > 0 && normalizedAnswer == Normalize(lemma);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant().Replace("，", ",").Replace("。", ".");
    }

    private static void ApplyKnownRateEma(UserWordRelationship relationship, AssessmentResult rating, bool isCorrect)
    {
        var delta = rating switch
        {
            AssessmentResult.Remembered when isCorrect => 0.15,
            AssessmentResult.Fuzzy => 0.05,
            AssessmentResult.Forgot => -0.2,
            _ when isCorrect => 0.08,
            _ => -0.1
        };
        relationship.EstimatedKnownRate = Math.Clamp(relationship.EstimatedKnownRate + delta, 0, 1);

        var personal = relationship.PersonalDifficulty ?? 50;
        if (rating == AssessmentResult.Forgot || !isCorrect)
        {
            relationship.PersonalDifficulty = Math.Clamp(personal + 5, 0, 100);
        }
        else if (rating == AssessmentResult.Remembered && isCorrect)
        {
            relationship.PersonalDifficulty = Math.Clamp(personal - 3, 0, 100);
        }

        relationship.PersonalUpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed record SubmitLearningRequest(
    Guid? UserId,
    Guid WordId,
    string Answer,
    AssessmentResult Rating,
    int ResponseTimeMs,
    /// <summary>T-014：考察模式 recognition（看词知义）/ recall（看义想词），缺省 recognition。</summary>
    string? Mode = null);

public sealed record LearningResultDto(
    bool IsCorrect,
    IReadOnlyList<string> Meanings,
    IReadOnlyList<string> ExampleSentences,
    /// <summary>T-014：掌握度由生命周期阶段派生（25/50/75/100），自评不再直接加减。</summary>
    double MasteryScore,
    DateTimeOffset NextReviewDue,
    int IntervalDays,
    /// <summary>T-014：当前生命周期阶段 token（recognized/recalled/prompted_use/spontaneous_use）。</summary>
    string Stage,
    /// <summary>T-014：下次考察模式（recognition/recall），随阶段切换。</summary>
    string QuizMode);
