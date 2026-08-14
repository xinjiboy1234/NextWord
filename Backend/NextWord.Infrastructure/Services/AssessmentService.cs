using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Scenarios;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using System.Text.Json;

namespace NextWord.Infrastructure.Services;

/// <summary>
/// 自适应分块测评（T-004，DESIGN-assessment-rework）：
/// 产出型题为主（≥60%：提示造句 ×2 + 情境表达 ×1），识别型（词义选择、阅读理解）降级为参考；
/// 产出题全部走 LLM 四维真实评分（复用 SentenceService 链路），主定级 = 表达力综合分；
/// 以当前估计水平为起点分块出题，表现好升带、差降带，2–3 块收敛，总题量 ≤15。
/// </summary>
public sealed class AssessmentService(
    ApplicationDbContext db,
    IAssessmentScoringService scoring,
    ISentenceService sentences,
    IUserRepository users,
    IScoreProfileService scoreProfile,
    IEvaluationReportService evaluationReports) : IAssessmentService
{
    private const int MaxBlocks = 3;
    private const string UnsubmittedMarker = "{}";
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

    public async Task<AssessmentBlockResponse> GetNextBlockAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assessment not found.");

        if (assessment.Status == AssessmentStatus.Completed)
        {
            return new AssessmentBlockResponse(true, null, RebuildFinal(assessment));
        }

        // GET 幂等：已生成但未提交的块直接重发，不重复出题
        var pending = assessment.Records
            .Where(item => item.Step == AssessmentStepType.AdaptiveBlock && item.ScoresJson == UnsubmittedMarker)
            .OrderByDescending(item => item.Timestamp)
            .FirstOrDefault();
        if (pending is not null)
        {
            var pendingPayload = JsonSerializer.Deserialize<BlockPayload>(pending.QuestionsJson, JsonOptions)!;
            return new AssessmentBlockResponse(false, ToView(pendingPayload), null);
        }

        var submittedScores = SubmittedBlocks(assessment);
        var blockIndex = submittedScores.Count + 1;
        var band = submittedScores.Count == 0
            ? await ResolveStartBandAsync(assessment.UserId, cancellationToken)
            : submittedScores[^1].Scores.NextBand;

        var payload = await BuildBlockAsync(blockIndex, band, cancellationToken);
        db.AssessmentRecords.Add(new AssessmentRecord
        {
            AssessmentId = assessmentId,
            Step = AssessmentStepType.AdaptiveBlock,
            QuestionType = $"block:{blockIndex}",
            QuestionsJson = JsonSerializer.Serialize(payload, JsonOptions),
            AnswersJson = "[]",
            ScoresJson = UnsubmittedMarker,
            ArticleId = payload.Reading?.ArticleId
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AssessmentBlockResponse(false, ToView(payload), null);
    }

    public async Task<AssessmentBlockResult> SubmitBlockAsync(
        Guid assessmentId,
        int blockIndex,
        IReadOnlyList<AssessmentAnswerItem> answers,
        CancellationToken cancellationToken)
    {
        var assessment = await db.Assessments
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken)
            ?? throw new InvalidOperationException("Assessment not found.");

        var record = assessment.Records.FirstOrDefault(item =>
            item.Step == AssessmentStepType.AdaptiveBlock && item.QuestionType == $"block:{blockIndex}")
            ?? throw new InvalidOperationException("Block not found.");
        var payload = JsonSerializer.Deserialize<BlockPayload>(record.QuestionsJson, JsonOptions)!;

        // 幂等重提交：直接回显已评分结果
        if (record.ScoresJson != UnsubmittedMarker)
        {
            var existingScores = JsonSerializer.Deserialize<BlockScores>(record.ScoresJson, JsonOptions)!;
            var converged = assessment.Status == AssessmentStatus.Completed;
            return new AssessmentBlockResult(
                converged,
                blockIndex,
                payload.Band,
                converged ? null : existingScores.NextBand,
                existingScores.BlockExpressionScore,
                converged ? RebuildFinal(assessment) : null);
        }

        // 产出题：全部走 LLM 四维真实评分（复用 SentenceService 链路），空作答记 0 不浪费调用
        var productionScores = new List<ProductionScore>();
        foreach (var item in payload.Production)
        {
            var text = answers.FirstOrDefault(answer => answer.Id == item.Id)?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                productionScores.Add(new ProductionScore(item.Id, 0, 0, 0, 0, 0, []));
                continue;
            }

            var log = item.Kind == "scenario"
                ? await sentences.RateAsync(assessment.UserId, null, "free expression", text, item.ScenarioKey ?? "life", payload.Band.ToString(), cancellationToken)
                : await sentences.RateAsync(assessment.UserId, item.WordId, item.TargetWord ?? string.Empty, text, "assessment", payload.Band.ToString(), cancellationToken);
            var score = scoring.ScoreProductionDimensions(log.GrammarScore, log.NaturalScore, log.VocabularyScore, log.RelevanceScore);
            // T-054：逐题 AI 评语/改写随测评记录持久化（旧记录无此字段，反序列化为 null）
            productionScores.Add(new ProductionScore(
                item.Id, score, log.GrammarScore, log.NaturalScore, log.VocabularyScore, log.RelevanceScore, log.ErrorTags,
                string.IsNullOrWhiteSpace(log.Suggestion) ? null : log.Suggestion,
                string.IsNullOrWhiteSpace(log.AiRevision) ? null : log.AiRevision));
        }

        // 识别题：只作参考信号，不进主定级；未作答（跳过）不计入样本（T-042：全跳过 = 样本缺失，防伪闸不矫正）
        var vocabScores = payload.Vocabulary
            .Select(item => (Item: item, SelectedIndex: answers.FirstOrDefault(answer => answer.Id == item.Id)?.SelectedIndex))
            .Where(pair => pair.SelectedIndex is not null)
            .Select(pair => new VocabScore(pair.Item.Id, pair.SelectedIndex == pair.Item.CorrectIndex))
            .ToList();
        ReadingScore? readingScore = null;
        if (payload.Reading is not null)
        {
            // 与词汇识别同口径（T-042）：未作答（跳过）不计样本
            var readingAnswer = answers.FirstOrDefault(answer => answer.Id == payload.Reading.Id);
            readingScore = readingAnswer?.SelectedIndex is int selectedIndex
                ? new ReadingScore(selectedIndex == payload.Reading.CorrectIndex, readingAnswer.LookupCount ?? 0)
                : null;
        }

        var blockExpression = productionScores.Count == 0 ? 0 : Math.Round(productionScores.Average(item => item.Score), 1);
        var decision = scoring.DecideBandMove(blockExpression);
        var nextBand = ApplyMove(payload.Band, decision);

        record.AnswersJson = JsonSerializer.Serialize(answers, JsonOptions);
        record.ScoresJson = JsonSerializer.Serialize(
            new BlockScores(true, blockExpression, productionScores, vocabScores, readingScore, decision, nextBand), JsonOptions);

        var completedBlocks = assessment.Records.Count(item => item.Step == AssessmentStepType.AdaptiveBlock && item.ScoresJson != UnsubmittedMarker);
        var shouldConverge = scoring.ShouldConverge(completedBlocks, decision);
        var final = shouldConverge ? await FinalizeAsync(assessment, cancellationToken) : null;

        await db.SaveChangesAsync(cancellationToken);
        return new AssessmentBlockResult(shouldConverge, blockIndex, payload.Band, shouldConverge ? null : nextBand, blockExpression, final);
    }

    public Task<Assessment?> GetAsync(Guid assessmentId, CancellationToken cancellationToken)
    {
        return db.Assessments
            .AsNoTracking()
            .Include(item => item.Records)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
    }

    public async Task<IReadOnlyList<AssessmentListItem>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var assessments = await db.Assessments
            .AsNoTracking()
            .Include(item => item.Records.Where(record => record.Step == AssessmentStepType.FinalLevel))
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.StartAt)
            .ToListAsync(cancellationToken);

        return assessments.Select(item =>
        {
            var final = item.Records.Count == 0
                ? null
                : JsonSerializer.Deserialize<AssessmentFinalResult>(item.Records[0].ScoresJson, JsonOptions);
            return new AssessmentListItem(
                item.Id,
                item.Type,
                item.Status,
                item.StartAt,
                item.EndAt,
                item.FinalLevel,
                final?.ExpressionScore,
                final?.OriginalLevelBeforeGuard is not null);
        }).ToList();
    }

    public async Task SkipInitialAsync(Guid userId, CancellationToken cancellationToken)
    {
        var inProgress = await db.Assessments
            .Where(item => item.UserId == userId
                && item.Type == AssessmentType.Initial
                && item.Status == AssessmentStatus.InProgress)
            .ToListAsync(cancellationToken);

        foreach (var assessment in inProgress)
        {
            assessment.Status = AssessmentStatus.Completed;
            assessment.EndAt = DateTimeOffset.UtcNow;
            assessment.FinalLevel = CefrLevel.A2;
        }

        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        progress.HasCompletedInitialAssessment = true;
        progress.OverallLevel = CefrLevel.A2;
        progress.VocabLevel = CefrLevel.A2;
        progress.SpellingLevel = CefrLevel.A2;
        progress.SentenceLevel = CefrLevel.A2;
        progress.ReadingLevel = CefrLevel.A2;
        progress.LevelStartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        await db.SaveChangesAsync(cancellationToken);
    }

    // ── 出题 ────────────────────────────────────────────────

    private async Task<BlockPayload> BuildBlockAsync(int blockIndex, CefrLevel band, CancellationToken cancellationToken)
    {
        // 测评词池纪律：utility=high/medium；low 永不入池
        var allWords = await db.Words.AsNoTracking()
            .Include(word => word.LlmAnnotation)
            .Include(word => word.Scenarios)
            .Where(word => word.Utility == WordUtility.High || word.Utility == WordUtility.Medium)
            .ToListAsync(cancellationToken);

        var bandWords = allWords.Where(word => word.CefrLevel == band).ToList();
        // 兜底：顶端带词池过薄（如 C1 仅个位数）时向下一带补充——仍不超当前带、不含 low
        if (bandWords.Count < 4 && band > CefrLevel.A1)
        {
            bandWords.AddRange(allWords.Where(word => word.CefrLevel == (CefrLevel)((int)band - 1)));
        }

        var shuffled = bandWords.OrderBy(_ => Random.Shared.Next()).ToList();

        // 提示造句 ×2：水平带内目标词
        var production = new List<ProductionItem>();
        var sentenceWords = shuffled.Take(2).ToList();
        while (sentenceWords.Count < 2 && shuffled.Count > 0)
        {
            // 词池极薄时允许重复用词，保证题量
            sentenceWords.Add(shuffled[Random.Shared.Next(shuffled.Count)]);
        }

        for (var i = 0; i < sentenceWords.Count; i++)
        {
            var word = sentenceWords[i];
            production.Add(new ProductionItem(
                $"s{i + 1}", "sentence", word.Id, word.Lemma, null, string.Empty,
                $"用「{word.Lemma}」造一个英文句子。", Intrinsic(word)));
        }

        // 情境表达 ×1：场景取自 I1 taxonomy，优先词池已标注场景
        var scenarioKeys = bandWords
            .SelectMany(word => word.Scenarios)
            .Select(link => link.ScenarioKey)
            .Where(ScenarioTaxonomy.IsSubScenarioKey)
            .Distinct()
            .ToList();
        var scenario = scenarioKeys.Count > 0
            ? ScenarioTaxonomy.Find(scenarioKeys[Random.Shared.Next(scenarioKeys.Count)])
            : ScenarioTaxonomy.All[Random.Shared.Next(ScenarioTaxonomy.All.Count)];
        production.Add(new ProductionItem(
            "e1", "scenario", null, null, scenario!.Key, scenario.ZhName,
            $"情境表达：在「{scenario.ZhName}」场景下，用英语写 2–3 句你的应对或感受。", 0));

        // 词义选择 ×1（参考）：本带词 + 本带干扰项，正确答案位置随机
        var vocabulary = new List<VocabItem>();
        var vocabWord = shuffled.FirstOrDefault(word => word.Meanings.Count > 0 && !sentenceWords.Contains(word))
            ?? shuffled.FirstOrDefault(word => word.Meanings.Count > 0);
        if (vocabWord is not null)
        {
            var correct = vocabWord.Meanings[0];
            var distractors = bandWords.Where(word => word.Id != vocabWord.Id && word.Meanings.Count > 0)
                .Select(word => word.Meanings[0])
                .Concat(allWords.Where(word => word.Id != vocabWord.Id && word.Meanings.Count > 0).Select(word => word.Meanings[0]))
                .Distinct()
                .Where(meaning => meaning != correct)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .ToList();
            while (distractors.Count < 3)
            {
                distractors.Add("未知含义");
            }

            var options = distractors.Append(correct).OrderBy(_ => Random.Shared.Next()).ToList();
            vocabulary.Add(new VocabItem("v1", vocabWord.Lemma, options, options.IndexOf(correct), Intrinsic(vocabWord)));
        }

        // 阅读理解 ×1（参考）：从库内文章按难度带选文，考点词来自正文，答案位置随机
        var reading = await BuildReadingItemAsync(band, shuffled, allWords, cancellationToken);

        return new BlockPayload(blockIndex, band, production, vocabulary, reading);
    }

    private async Task<ReadingItem?> BuildReadingItemAsync(
        CefrLevel band,
        List<Word> bandWords,
        List<Word> allWords,
        CancellationToken cancellationToken)
    {
        var articles = await db.Articles.AsNoTracking().ToListAsync(cancellationToken);
        if (articles.Count == 0)
        {
            return null;
        }

        // 按难度带就近 + 随机排序，跳过正文中找不到考点词的文章
        var candidates = articles
            .OrderBy(item => Math.Abs((int)item.CefrLevel - (int)band))
            .ThenBy(_ => Random.Shared.Next())
            .ToList();

        foreach (var article in candidates)
        {
            var tokens = article.Content
                .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '"', '(', ')', '’', '\''], StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim().ToLowerInvariant())
                .ToHashSet();

            // 考点词：优先本带词池，其次全池；必须出现在正文中且为单词（非短语）
            var keyWord = bandWords.Where(word => word.Meanings.Count > 0 && !word.Lemma.Contains(' ') && tokens.Contains(word.Lemma.ToLowerInvariant()))
                    .OrderBy(_ => Random.Shared.Next())
                    .FirstOrDefault()
                ?? allWords.Where(word => word.Meanings.Count > 0 && !word.Lemma.Contains(' ') && tokens.Contains(word.Lemma.ToLowerInvariant()))
                    .OrderBy(_ => Random.Shared.Next())
                    .FirstOrDefault();
            if (keyWord is null)
            {
                continue;
            }

            var correct = keyWord.Meanings[0];
            var options = allWords
                .Where(word => word.Id != keyWord.Id && word.Meanings.Count > 0)
                .Select(word => word.Meanings[0])
                .Distinct()
                .Where(meaning => meaning != correct)
                .OrderBy(_ => Random.Shared.Next())
                .Take(3)
                .Append(correct)
                .OrderBy(_ => Random.Shared.Next())
                .ToList();

            return new ReadingItem(
                "r1",
                article.Id,
                article.Title,
                article.Content,
                article.WordCount,
                $"文中 \"{keyWord.Lemma}\" 的含义是什么？",
                options,
                options.IndexOf(correct));
        }

        return null;
    }

    // ── 定级 ────────────────────────────────────────────────

    private async Task<AssessmentFinalResult> FinalizeAsync(Assessment assessment, CancellationToken cancellationToken)
    {
        var blocks = SubmittedBlocks(assessment);
        var production = blocks.SelectMany(item => item.Scores.Production).ToList();
        var vocabulary = blocks.SelectMany(item => item.Scores.Vocabulary).ToList();
        var readings = blocks.Select(item => item.Scores.Reading).Where(item => item is not null).ToList();

        var expressionComposite = production.Count == 0 ? 0 : production.Average(item => item.Score);
        var expressionScore = Math.Clamp((int)Math.Round(expressionComposite), 0, 100);
        var expressionLevel = scoring.MapExpressionScore(expressionComposite);

        // 识别题：参考信号，不进主定级
        var vocabAccuracy = vocabulary.Count == 0 ? 0 : vocabulary.Count(item => item.Correct) * 100.0 / vocabulary.Count;
        var readingCorrect = readings.Count(item => item!.Correct);
        var readingAccuracy = readings.Count == 0 ? 0 : readingCorrect * 100.0 / readings.Count;
        var lookupCount = readings.Sum(item => item!.LookupCount);
        var readingWordCount = blocks
            .Select(item => item.Payload.Reading?.WordCount ?? 0)
            .Sum();
        var vocabReferenceScore = scoring.MapVocabToScore(vocabAccuracy);
        var readingReferenceScore = scoring.MapReadingToScore(readingAccuracy, lookupCount, Math.Max(readingWordCount, 1));
        var vocabReferenceLevel = scoring.MapVocabAccuracy(vocabAccuracy);
        var readingReferenceLevel = scoring.MapReadingAccuracy(readingAccuracy, lookupCount, Math.Max(readingWordCount, 1));

        // T-042 识别防伪闸：表达定级档 − 词汇识别参考档 ≥2 时下调 1 档（一次性、下限 A1）；
        // 识别样本缺失（无词汇识别作答）不矫正；识别不加权进表达分，只做矫正信号
        var (overall, guardAdjusted) = scoring.ApplyRecognitionGuard(
            expressionLevel, vocabulary.Count == 0 ? null : vocabReferenceLevel);

        var grammar = production.Count == 0 ? 0 : production.Average(item => item.Grammar);
        var natural = production.Count == 0 ? 0 : production.Average(item => item.Natural);
        var vocabDim = production.Count == 0 ? 0 : production.Average(item => item.Vocabulary);
        var relevance = production.Count == 0 ? 0 : production.Average(item => item.Relevance);
        var topErrorTags = production
            .SelectMany(item => item.ErrorTags)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .GroupBy(tag => tag)
            .OrderByDescending(group => group.Count())
            .Take(5)
            .Select(group => group.Key)
            .ToList();
        var comments = new List<string>
        {
            $"表达力综合分 {expressionScore}/100：语法 {grammar:0.0}、自然度 {natural:0.0}、词汇 {vocabDim:0.0}、相关度 {relevance:0.0}（各维度满分 5）。",
            guardAdjusted
                ? $"表达表现 {expressionLevel}，综合词汇掌握情况调整为 {overall}。"
                : $"主定级 {overall} 由表达力综合分决定；词汇识别 {vocabReferenceScore}、阅读 {readingReferenceScore} 仅作参考，不影响定级。"
        };
        var dimensions = new AssessmentDimensionSummary(
            Math.Round(grammar, 1), Math.Round(natural, 1), Math.Round(vocabDim, 1), Math.Round(relevance, 1),
            topErrorTags, comments);

        // T-055 人话 rubric（DESIGN-assessment-visibility §3.1）：总体标签按表达综合分分带
        // （expressionLevel 已复用 ScoreMapping:CefrBands 派生；防伪闸矫正只调等级外壳，不改变表达表现本身），
        // 四维按 0–5 三档给人话特征描述，随定级结果持久化（旧记录无此字段，前端降级不显示）
        var overallRubric = ProficiencyRubric.DescribeOverall(expressionLevel);
        var rubric = new ProficiencyRubricView(
            overallRubric.Label,
            overallRubric.Description,
            [
                new RubricDimensionView(ProficiencyRubric.DimensionName(RubricDimension.Grammar), Math.Round(grammar, 1), ProficiencyRubric.DescribeDimension(RubricDimension.Grammar, grammar)),
                new RubricDimensionView(ProficiencyRubric.DimensionName(RubricDimension.Natural), Math.Round(natural, 1), ProficiencyRubric.DescribeDimension(RubricDimension.Natural, natural)),
                new RubricDimensionView(ProficiencyRubric.DimensionName(RubricDimension.Vocabulary), Math.Round(vocabDim, 1), ProficiencyRubric.DescribeDimension(RubricDimension.Vocabulary, vocabDim)),
                new RubricDimensionView(ProficiencyRubric.DimensionName(RubricDimension.Relevance), Math.Round(relevance, 1), ProficiencyRubric.DescribeDimension(RubricDimension.Relevance, relevance))
            ]);

        var final = new AssessmentFinalResult(
            overall,
            expressionScore,
            vocabReferenceScore,
            readingReferenceScore,
            vocabReferenceLevel,
            readingReferenceLevel,
            dimensions,
            null,
            guardAdjusted ? expressionLevel : null,
            rubric);

        assessment.Status = AssessmentStatus.Completed;
        assessment.EndAt = DateTimeOffset.UtcNow;
        assessment.FinalLevel = overall;

        var previous = (await users.GetOrCreateProgressAsync(assessment.UserId, cancellationToken)).OverallLevel;
        // 表达优先：档案各维度分数统一以表达力综合分为初始先验；识别题只进结果展示与画像内核，
        // 不写权威分，避免「最短板 min」把识别参考分低的好表达者拖低（DESIGN-assessment-rework §2.2）。
        // T-042 矫正传导：防伪闸触发时三维先验逐维 clamp 到矫正后档上限以内（保持相对形状），
        // 否则 CefrDisplay 仍按矫正前虚高档，Planner 词池/造句目标取词带错位（qa-t042 P1）
        var priorScore = guardAdjusted ? Math.Min(expressionScore, scoring.GetBandScoreCeiling(overall)) : expressionScore;
        // T-038：测评定级写入是权威锚点，cefrDisplay 不走下行迟滞（含 T-042 矫正传导的下调）
        await scoreProfile.ApplyUpdateAsync(
            new ProfileUpdateCommand(
                assessment.UserId,
                "AssessmentCompleted",
                new ProfileScoreAssignment(priorScore, priorScore, priorScore, null),
                null,
                $"assessment:{assessment.Id}:complete",
                JsonSerializer.Serialize(final, JsonOptions),
                BypassCefrDisplayHysteresis: true),
            cancellationToken);

        // 等级外壳以测评主定级为准（内核 min 语义不适用于表达优先的初测）
        var progress = await users.GetOrCreateProgressAsync(assessment.UserId, cancellationToken);
        progress.HasCompletedInitialAssessment = true;
        progress.OverallLevel = overall;
        progress.SentenceLevel = overall;
        progress.VocabLevel = scoring.MapVocabAccuracy(vocabAccuracy);
        progress.ReadingLevel = scoring.MapReadingAccuracy(readingAccuracy, lookupCount, Math.Max(readingWordCount, 1));
        progress.LevelStartDate = DateOnly.FromDateTime(DateTime.UtcNow);

        db.LevelHistories.Add(new LevelHistory
        {
            UserId = assessment.UserId,
            FromLevel = previous,
            ToLevel = overall,
            Reason = LevelChangeReason.Initial
        });

        await db.SaveChangesAsync(cancellationToken);

        var reportId = await evaluationReports.EnqueueForUserAsync(assessment.UserId, "InitialAssessment", assessment.Id, cancellationToken);
        final = final with { EvaluationReportId = reportId };

        db.AssessmentRecords.Add(new AssessmentRecord
        {
            AssessmentId = assessment.Id,
            Step = AssessmentStepType.FinalLevel,
            QuestionType = "final",
            QuestionsJson = "{}",
            AnswersJson = "{}",
            ScoresJson = JsonSerializer.Serialize(final, JsonOptions)
        });

        return final;
    }

    // ── 工具 ────────────────────────────────────────────────

    private async Task<CefrLevel> ResolveStartBandAsync(Guid userId, CancellationToken cancellationToken)
    {
        var progress = await users.GetOrCreateProgressAsync(userId, cancellationToken);
        // 从未测评的用户 OverallLevel 只是 A1 占位默认值，以 A2 为起点估计（与 skip 默认一致）
        return ClampBand(progress.HasCompletedInitialAssessment ? progress.OverallLevel : CefrLevel.A2);
    }

    private List<(BlockPayload Payload, BlockScores Scores)> SubmittedBlocks(Assessment assessment)
    {
        return assessment.Records
            .Where(item => item.Step == AssessmentStepType.AdaptiveBlock && item.ScoresJson != UnsubmittedMarker)
            .OrderBy(item => item.Timestamp)
            .Select(item => (
                JsonSerializer.Deserialize<BlockPayload>(item.QuestionsJson, JsonOptions)!,
                JsonSerializer.Deserialize<BlockScores>(item.ScoresJson, JsonOptions)!))
            .ToList();
    }

    private AssessmentFinalResult? RebuildFinal(Assessment assessment)
    {
        var record = assessment.Records.FirstOrDefault(item => item.Step == AssessmentStepType.FinalLevel);
        return record is null ? null : JsonSerializer.Deserialize<AssessmentFinalResult>(record.ScoresJson, JsonOptions);
    }

    private static AssessmentBlockView ToView(BlockPayload payload)
    {
        return new AssessmentBlockView(
            payload.BlockIndex,
            MaxBlocks,
            payload.Band,
            payload.Production.Select(item => new AssessmentProductionPrompt(item.Id, item.Kind, item.TargetWord, item.ScenarioZh, item.Prompt)).ToList(),
            payload.Vocabulary.Select(item => new AssessmentVocabChoice(item.Id, item.Word, item.Options)).ToList(),
            payload.Reading is null
                ? null
                : new AssessmentReadingItem(payload.Reading.Id, payload.Reading.Title, payload.Reading.Content, payload.Reading.Question, payload.Reading.Options));
    }

    private static int Intrinsic(Word word) =>
        word.LlmAnnotation?.IntrinsicScore ?? LegacyScoreHelper.FromDifficulty(word.DifficultyLevel);

    private static CefrLevel ApplyMove(CefrLevel band, BandMove move) => ClampBand((CefrLevel)((int)band + (int)move));

    private static CefrLevel ClampBand(CefrLevel level) =>
        level < CefrLevel.A1 ? CefrLevel.A1 : level > CefrLevel.C1 ? CefrLevel.C1 : level;

    // ── 持久化载荷 ───────────────────────────────────────────

    private sealed record BlockPayload(
        int BlockIndex,
        CefrLevel Band,
        List<ProductionItem> Production,
        List<VocabItem> Vocabulary,
        ReadingItem? Reading);

    private sealed record ProductionItem(
        string Id,
        string Kind,
        Guid? WordId,
        string? TargetWord,
        string? ScenarioKey,
        string ScenarioZh,
        string Prompt,
        int IntrinsicScore);

    private sealed record VocabItem(string Id, string Word, List<string> Options, int CorrectIndex, int IntrinsicScore);

    private sealed record ReadingItem(
        string Id,
        Guid ArticleId,
        string Title,
        string Content,
        int WordCount,
        string Question,
        List<string> Options,
        int CorrectIndex);

    private sealed record BlockScores(
        bool Submitted,
        double BlockExpressionScore,
        List<ProductionScore> Production,
        List<VocabScore> Vocabulary,
        ReadingScore? Reading,
        BandMove Decision,
        CefrLevel NextBand);

    /// <summary>T-054 起新增可空 Suggestion/AiRevision（逐题 AI 评语）；旧记录 JSON 无此属性，反序列化为 null。</summary>
    private sealed record ProductionScore(
        string Id,
        double Score,
        int Grammar,
        int Natural,
        int Vocabulary,
        int Relevance,
        List<string> ErrorTags,
        string? Suggestion = null,
        string? AiRevision = null);

    private sealed record VocabScore(string Id, bool Correct);

    private sealed record ReadingScore(bool Correct, int LookupCount);
}
