using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;
using System.Text.Json;

namespace NextWord.Infrastructure.Services;

public sealed class AssessmentService(
    ApplicationDbContext db,
    IAssessmentScoringService scoring,
    ISentenceService sentences,
    IUserRepository users,
    IScoreProfileService scoreProfile,
    IBackgroundJobService backgroundJobs,
    IEvaluationReportService evaluationReports) : IAssessmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Assessment> StartInitialAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.Assessments
            .FirstOrDefaultAsync(item => item.UserId == userId && item.Type == AssessmentType.Initial && item.Status == AssessmentStatus.InProgress, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var assessment = new Assessment
        {
            UserId = userId,
            Type = AssessmentType.Initial,
            Status = AssessmentStatus.InProgress
        };
        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);
        return assessment;
    }

    public async Task<object> GetStepQuestionsAsync(Guid assessmentId, AssessmentStepType step, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assessment not found.");

        return step switch
        {
            AssessmentStepType.Vocabulary => await BuildVocabQuestionsAsync(cancellationToken),
            AssessmentStepType.Spelling => await BuildSpellingQuestionsAsync(cancellationToken),
            AssessmentStepType.Sentence => await BuildSentenceQuestionsAsync(cancellationToken),
            AssessmentStepType.Reading => await BuildReadingQuestionAsync(cancellationToken),
            AssessmentStepType.FinalLevel => new { message = "Submit all previous steps first." },
            _ => throw new InvalidOperationException("Unsupported step.")
        };
    }

    public async Task<StepScoreResult> SubmitStepAsync(Guid assessmentId, AssessmentStepType step, string answersJson, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assessment not found.");

        var questions = await GetStepQuestionsAsync(assessmentId, step, cancellationToken);
        var questionsJson = JsonSerializer.Serialize(questions, JsonOptions);
        var result = ScoreStep(step, questions, answersJson);

        var record = assessment.Records.FirstOrDefault(item => item.Step == step);
        if (record is null)
        {
            record = new AssessmentRecord
            {
                AssessmentId = assessmentId,
                Step = step,
                QuestionType = step.ToString()
            };
            db.AssessmentRecords.Add(record);
            assessment.Records.Add(record);
        }

        record.QuestionsJson = questionsJson;
        record.AnswersJson = answersJson;
        record.ScoresJson = MergeMappedLevel(result);
        if (step == AssessmentStepType.Reading && questions is ReadingStepPayload reading)
        {
            record.ArticleId = reading.ArticleId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<FinalLevelResult?> CompleteInitialAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assessment not found.");

        var required = new[] { AssessmentStepType.Vocabulary, AssessmentStepType.Spelling, AssessmentStepType.Sentence, AssessmentStepType.Reading };
        if (required.Any(step => assessment.Records.All(record => record.Step != step)))
        {
            return null;
        }

        var vocab = assessment.Records.First(record => record.Step == AssessmentStepType.Vocabulary);
        var spelling = assessment.Records.First(record => record.Step == AssessmentStepType.Spelling);
        var sentence = assessment.Records.First(record => record.Step == AssessmentStepType.Sentence);
        var reading = assessment.Records.First(record => record.Step == AssessmentStepType.Reading);

        var final = scoring.CalculateFinalLevel(
            RebuildStep(vocab, AssessmentStepType.Vocabulary),
            RebuildStep(spelling, AssessmentStepType.Spelling),
            RebuildStep(sentence, AssessmentStepType.Sentence),
            RebuildStep(reading, AssessmentStepType.Reading));

        var scoreResult = scoring.CalculateFinalScores(
            RebuildStep(vocab, AssessmentStepType.Vocabulary),
            RebuildStep(spelling, AssessmentStepType.Spelling),
            RebuildStep(sentence, AssessmentStepType.Sentence),
            RebuildStep(reading, AssessmentStepType.Reading));

        db.AssessmentRecords.Add(new AssessmentRecord
        {
            AssessmentId = assessmentId,
            Step = AssessmentStepType.FinalLevel,
            QuestionType = "final",
            QuestionsJson = "{}",
            AnswersJson = "{}",
            ScoresJson = JsonSerializer.Serialize(new { final, scoreResult }, JsonOptions)
        });

        assessment.Status = AssessmentStatus.Completed;
        assessment.EndAt = DateTimeOffset.UtcNow;
        assessment.FinalLevel = final.OverallLevel;

        var previous = (await users.GetOrCreateProgressAsync(assessment.UserId, cancellationToken)).OverallLevel;

        await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                assessment.UserId,
                "AssessmentCompleted",
                new ProfileScoreAssignment(
                    scoreResult.VocabularyScore,
                    scoreResult.ReadingScore,
                    scoreResult.WritingScore,
                    scoreResult.SpellingScore),
                null,
                $"assessment:{assessmentId}:complete",
                JsonSerializer.Serialize(new { final, scoreResult }, JsonOptions)),
            cancellationToken);

        var progress = await users.GetOrCreateProgressAsync(assessment.UserId, cancellationToken);
        progress.HasCompletedInitialAssessment = true;
        progress.LevelStartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        db.LevelHistories.Add(new LevelHistory
        {
            UserId = assessment.UserId,
            FromLevel = previous,
            ToLevel = final.OverallLevel,
            Reason = LevelChangeReason.Initial
        });

        await db.SaveChangesAsync(cancellationToken);

        var evaluationReportId = await evaluationReports.EnqueueForUserAsync(
            assessment.UserId,
            "InitialAssessment",
            assessmentId,
            cancellationToken);

        var sentencePayload = JsonSerializer.Serialize(new
        {
            userId = assessment.UserId,
            assessmentId,
            answers = JsonSerializer.Deserialize<List<string>>(sentence.AnswersJson, JsonOptions)?
                .Zip(JsonSerializer.Deserialize<List<SentenceQuizQuestion>>(sentence.QuestionsJson, JsonOptions) ?? [])
                .Select(pair => new
                {
                    WordId = pair.Second?.WordId,
                    TargetWord = pair.Second?.Word ?? string.Empty,
                    Scene = pair.Second?.Scene ?? "life",
                    Answer = pair.First ?? string.Empty
                }).ToList()
        }, JsonOptions);

        await backgroundJobs.EnqueueAsync(
            "SentenceLlmScoring",
            sentencePayload,
            $"sentence:assessment:{assessmentId}",
            cancellationToken);

        return final with
        {
            VocabularyScore = scoreResult.VocabularyScore,
            SpellingScore = scoreResult.SpellingScore,
            WritingScore = scoreResult.WritingScore,
            ReadingScore = scoreResult.ReadingScore,
            OverallScore = scoreResult.OverallScore,
            EvaluationReportId = evaluationReportId
        };
    }

    public Task<Assessment?> GetAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        return db.Assessments
            .AsNoTracking()
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
    }

    private async Task<IReadOnlyList<VocabQuizQuestion>> BuildVocabQuestionsAsync(CancellationToken cancellationToken)
    {
        var words = (await db.Words.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(_ => Random.Shared.Next())
            .Take(12)
            .ToList();
        return words.Select(word =>
        {
            var correct = word.Meanings.FirstOrDefault() ?? word.Lemma;
            var options = words.Where(item => item.Id != word.Id)
                .Select(item => item.Meanings.FirstOrDefault() ?? item.Lemma)
                .Distinct()
                .Take(3)
                .ToList();
            while (options.Count < 3) options.Add("unknown");
            var all = new List<string> { correct };
            all.AddRange(options);
            all = all.OrderBy(_ => Guid.NewGuid()).ToList();
            var correctIndex = all.FindIndex(item => item == correct);
            return new VocabQuizQuestion(word.Lemma, all, Math.Max(0, correctIndex), word.DifficultyLevel);
        }).ToList();
    }

    private async Task<IReadOnlyList<SpellingQuizQuestion>> BuildSpellingQuestionsAsync(CancellationToken cancellationToken)
    {
        var words = (await db.Words.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(_ => Random.Shared.Next())
            .Take(10)
            .ToList();
        return words.Select(word => new SpellingQuizQuestion(
            word.Meanings.FirstOrDefault() ?? word.Lemma,
            word.Lemma,
            word.DifficultyLevel)).ToList();
    }

    private async Task<IReadOnlyList<SentenceQuizQuestion>> BuildSentenceQuestionsAsync(CancellationToken cancellationToken)
    {
        var prompts = await sentences.GetPromptsAsync(3, cancellationToken);
        return prompts.Select(item => new SentenceQuizQuestion(item.WordId, item.TargetWord, item.Scene)).ToList();
    }

    private async Task<ReadingStepPayload> BuildReadingQuestionAsync(CancellationToken cancellationToken)
    {
        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        var article = articles.OrderBy(_ => Random.Shared.Next()).First();
        return new ReadingStepPayload(
            article.Id,
            article.Title,
            article.Content,
            article.WordCount,
            new ReadingQuizQuestion(
                article.Id,
                "What topic does this article mainly discuss?",
                ["Learning and daily life", "Space travel", "Ancient history", "Machine repair"],
                0,
                article.Content.Length > 300 ? article.Content[..300] + "..." : article.Content));
    }

    private StepScoreResult ScoreStep(AssessmentStepType step, object questions, string answersJson)
    {
        return step switch
        {
            AssessmentStepType.Vocabulary => ScoreVocab(questions, answersJson),
            AssessmentStepType.Spelling => ScoreSpelling(questions, answersJson),
            AssessmentStepType.Sentence => ScoreSentence(answersJson),
            AssessmentStepType.Reading => ScoreReading(questions, answersJson),
            _ => new StepScoreResult(step, null, 0, "{}")
        };
    }

    private StepScoreResult ScoreVocab(object questions, string answersJson)
    {
        var qs = ((IEnumerable<VocabQuizQuestion>)questions).ToList();
        var answers = JsonSerializer.Deserialize<List<int>>(answersJson, JsonOptions) ?? [];
        var correct = qs.Select((q, i) => i < answers.Count && answers[i] == q.CorrectIndex).Count(match => match);
        var accuracy = qs.Count == 0 ? 0 : (double)correct / qs.Count * 100;
        var level = scoring.MapVocabAccuracy(accuracy);
        var scores = new { basic_correct = correct, total = qs.Count, accuracy };
        return new StepScoreResult(AssessmentStepType.Vocabulary, level, accuracy, JsonSerializer.Serialize(scores, JsonOptions));
    }

    private StepScoreResult ScoreSpelling(object questions, string answersJson)
    {
        var qs = ((IEnumerable<SpellingQuizQuestion>)questions).ToList();
        var answers = JsonSerializer.Deserialize<List<string>>(answersJson, JsonOptions) ?? [];
        var correct = qs.Select((q, i) => i < answers.Count && string.Equals(answers[i]?.Trim(), q.CorrectSpelling, StringComparison.OrdinalIgnoreCase)).Count(match => match);
        var accuracy = qs.Count == 0 ? 0 : (double)correct / qs.Count * 100;
        var level = scoring.MapSpellingAccuracy(accuracy);
        var scores = new { correct, total = qs.Count, accuracy };
        return new StepScoreResult(AssessmentStepType.Spelling, level, accuracy, JsonSerializer.Serialize(scores, JsonOptions));
    }

    private StepScoreResult ScoreSentence(string answersJson)
    {
        // 初测造句步采用答案长度与完整性启发式；正式 LLM 评分在 SentenceService 中
        var answers = JsonSerializer.Deserialize<List<string>>(answersJson, JsonOptions) ?? [];
        var scores = answers.Select(answer =>
        {
            var trimmed = answer?.Trim() ?? string.Empty;
            var wordCount = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Clamp(wordCount >= 6 ? 3.5 : wordCount >= 4 ? 2.5 : 1.5, 0, 5);
        }).ToList();
        var average = scores.Count == 0 ? 0 : scores.Average();
        var level = scoring.MapSentenceAverage(average);
        return new StepScoreResult(AssessmentStepType.Sentence, level, average, JsonSerializer.Serialize(new { average, scores }, JsonOptions));
    }

    private StepScoreResult ScoreReading(object questions, string answersJson)
    {
        var payload = (ReadingStepPayload)questions;
        var answer = JsonSerializer.Deserialize<ReadingAnswerPayload>(answersJson, JsonOptions) ?? new ReadingAnswerPayload(0, 0);
        var correct = answer.SelectedIndex == payload.Question.CorrectIndex ? 1 : 0;
        var accuracy = correct * 100.0;
        var level = scoring.MapReadingAccuracy(accuracy, answer.LookupCount, payload.WordCount);
        var scores = new { correct, accuracy, lookupCount = answer.LookupCount };
        return new StepScoreResult(AssessmentStepType.Reading, level, accuracy, JsonSerializer.Serialize(scores, JsonOptions));
    }

    private StepScoreResult RebuildStep(AssessmentRecord record, AssessmentStepType step)
    {
        using var doc = JsonDocument.Parse(record.ScoresJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("mappedLevel", out var mapped) &&
            Enum.TryParse<CefrLevel>(mapped.GetString(), out var parsedLevel))
        {
            var raw = root.TryGetProperty("accuracy", out var accuracy) ? accuracy.GetDouble()
                : root.TryGetProperty("average", out var average) ? average.GetDouble() : 0;
            return new StepScoreResult(step, parsedLevel, raw, record.ScoresJson);
        }

        return step switch
        {
            AssessmentStepType.Vocabulary when root.TryGetProperty("accuracy", out var accuracy)
                => new StepScoreResult(step, scoring.MapVocabAccuracy(accuracy.GetDouble()), accuracy.GetDouble(), record.ScoresJson),
            AssessmentStepType.Spelling when root.TryGetProperty("accuracy", out var accuracy)
                => new StepScoreResult(step, scoring.MapSpellingAccuracy(accuracy.GetDouble()), accuracy.GetDouble(), record.ScoresJson),
            AssessmentStepType.Sentence when root.TryGetProperty("average", out var average)
                => new StepScoreResult(step, scoring.MapSentenceAverage(average.GetDouble()), average.GetDouble(), record.ScoresJson),
            AssessmentStepType.Reading when root.TryGetProperty("accuracy", out var accuracy)
                => new StepScoreResult(step, scoring.MapReadingAccuracy(accuracy.GetDouble(), root.TryGetProperty("lookupCount", out var lookups) ? lookups.GetInt32() : 0, 120), accuracy.GetDouble(), record.ScoresJson),
            _ => new StepScoreResult(step, CefrLevel.A1, 0, record.ScoresJson)
        };
    }

    private static string MergeMappedLevel(StepScoreResult result)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(result.ScoresJson, JsonOptions)
            ?? new Dictionary<string, JsonElement>();
        var mutable = dict.ToDictionary(pair => pair.Key, pair => pair.Value);
        mutable["mappedLevel"] = JsonSerializer.SerializeToElement(result.MappedLevel?.ToString() ?? CefrLevel.A1.ToString(), JsonOptions);
        return JsonSerializer.Serialize(mutable, JsonOptions);
    }

    private sealed record ReadingStepPayload(Guid ArticleId, string Title, string Content, int WordCount, ReadingQuizQuestion Question);
    private sealed record ReadingAnswerPayload(int SelectedIndex, int LookupCount);
}
