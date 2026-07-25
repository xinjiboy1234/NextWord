using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Domain.Services;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;

namespace NextWord.UnitTests;

/// <summary>
/// T-005 WeaknessProfile + Verifier（真实 PG）：
/// 全链路生成持久化与幂等；Verifier 机械核查——伪造引用、篡改数值、置信度样本量不足均标「存疑」；
/// 评估报告内容在测评触发时切换为已验证 Finding 列表（schemaVersion 2），无测评关联时回退模板。
/// </summary>
public class WeaknessProfileTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Generate_persists_verified_findings_and_is_idempotent()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-happy");
        var logs = await SeedSentenceLogsAsync(db, user.Id, [4, 4, 4]);
        var assessment = await SeedAssessmentWithFinalAsync(db, user.Id, grammar: 4.0, natural: 4.0, vocabulary: 3.0, relevance: 4.0);
        var (correctRate, avgLookup) = await SeedStatsAsync(db, user.Id, "happy");

        var llm = new StubProfilerLlm(_ => TruthfulDrafts(logs, correctRate, avgLookup));
        var service = CreateProfileService(db, llm);

        var profile = await service.GenerateAsync(user.Id, assessment.Id, CancellationToken.None);

        Assert.True(profile.Id > 0);
        Assert.Equal(3, profile.Findings.Count);
        Assert.All(profile.Findings, finding => Assert.Equal(FindingVerification.Verified, finding.Verification));
        Assert.Contains(profile.Findings, finding => finding.Dimension == FindingDimension.Skill && finding.Confidence == FindingConfidence.High);
        Assert.Contains(profile.Findings, finding => finding.Dimension == FindingDimension.Scenario && finding.DimensionKey == "dining_out");
        // 证据引用可回溯：skill 条目引用真实 SentenceLog id
        var skill = profile.Findings.First(finding => finding.Dimension == FindingDimension.Skill);
        var evidence = JsonSerializer.Deserialize<List<EvidenceClaim>>(skill.EvidenceJson, JsonOptions)!;
        Assert.All(evidence, claim => Assert.Contains(logs, log => log.Id == Guid.Parse(claim.RefId)));

        // 幂等：同一测评重复生成直接返回已有画像，不产生新行
        var again = await service.GenerateAsync(user.Id, assessment.Id, CancellationToken.None);
        Assert.Equal(profile.Id, again.Id);
        Assert.Equal(1, await db.WeaknessProfiles.CountAsync(item => item.UserId == user.Id));

        var latest = await service.GetLatestAsync(user.Id, CancellationToken.None);
        Assert.Equal(profile.Id, latest!.Id);
    }

    [Fact]
    public async Task Verifier_flags_forged_and_tampered_evidence_as_questioned()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-tamper");
        var other = await SeedUserAsync(db, "profile-tamper-other");
        var ownLogs = await SeedSentenceLogsAsync(db, user.Id, [4]);
        var otherLogs = await SeedSentenceLogsAsync(db, other.Id, [1]);
        var assessment = await SeedAssessmentWithFinalAsync(db, user.Id, grammar: 4.0, natural: 4.0, vocabulary: 4.0, relevance: 4.0);
        await SeedStatsAsync(db, user.Id, "tamper");

        var truthful = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Strength, "语法表现稳定。",
            [new EvidenceClaim("sentence_log", ownLogs[0].Id.ToString(), "grammar", "<=", 4)],
            FindingConfidence.Low);
        var forgedRef = truthful with
        {
            Statement = "伪造引用的结论。",
            Evidence = [new EvidenceClaim("sentence_log", Guid.NewGuid().ToString(), "grammar", "<=", 4)]
        };
        var stolenRef = truthful with
        {
            Statement = "引用他人记录的结论。",
            Evidence = [new EvidenceClaim("sentence_log", otherLogs[0].Id.ToString(), "grammar", "<=", 1)]
        };
        var tamperedValue = truthful with
        {
            Statement = "篡改数值的结论。",
            Evidence = [new EvidenceClaim("sentence_log", ownLogs[0].Id.ToString(), "grammar", "<=", 1)]
        };
        var unsupportedConfidence = truthful with
        {
            Statement = "样本量不足的结论。",
            Confidence = FindingConfidence.High
        };
        var emptyEvidence = truthful with { Statement = "无证据的结论。", Evidence = [] };

        var verifier = new FindingVerifier(db);
        var results = await verifier.VerifyAsync(
            user.Id,
            assessment.Id,
            [truthful, forgedRef, stolenRef, tamperedValue, unsupportedConfidence, emptyEvidence],
            CancellationToken.None);

        Assert.Equal(FindingVerification.Verified, results[0].Verification);
        Assert.Equal(FindingVerification.Questioned, results[1].Verification);
        Assert.Contains("不存在", results[1].Note);
        Assert.Equal(FindingVerification.Questioned, results[2].Verification);
        Assert.Contains("不存在", results[2].Note); // 他人记录按 UserId 过滤后查不到
        Assert.Equal(FindingVerification.Questioned, results[3].Verification);
        Assert.Contains("不属实", results[3].Note);
        Assert.Equal(FindingVerification.Questioned, results[4].Verification);
        Assert.Contains("样本量不足", results[4].Note);
        Assert.Equal(FindingVerification.Questioned, results[5].Verification);
        Assert.Contains("无证据引用", results[5].Note);

        // 全链路：LLM 产出掺假草稿 → 持久化后仅真实条目为 Verified
        // （T-010：同维度草稿会在核查前去重，这里用不同 dimensionKey 走核查路径）
        var llm = new StubProfilerLlm(_ =>
        [
            truthful,
            forgedRef with { DimensionKey = "natural" },
            // 篡改数值走另一 metric，避免与 truthful 的证据 key 相同而在核查前去重阶段被剥夺
            tamperedValue with
            {
                DimensionKey = "vocabulary",
                Evidence = [new EvidenceClaim("sentence_log", ownLogs[0].Id.ToString(), "natural", "<=", 1)]
            }
        ]);
        var service = CreateProfileService(db, llm);
        var profile = await service.GenerateAsync(user.Id, assessment.Id, CancellationToken.None);
        Assert.Equal(3, profile.Findings.Count);
        Assert.Single(profile.Findings, finding => finding.Verification == FindingVerification.Verified);
        Assert.Equal(2, profile.Findings.Count(finding => finding.Verification == FindingVerification.Questioned));
    }

    [Fact]
    public async Task Report_switches_to_verified_findings_after_assessment()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-report");
        var logs = await SeedSentenceLogsAsync(db, user.Id, [4, 4, 4]);
        var assessment = await SeedAssessmentWithFinalAsync(db, user.Id, grammar: 4.0, natural: 4.0, vocabulary: 3.0, relevance: 4.0);
        var (correctRate, avgLookup) = await SeedStatsAsync(db, user.Id, "report");

        var llm = new StubProfilerLlm(_ => TruthfulDrafts(logs, correctRate, avgLookup));
        var reportService = CreateReportService(db, llm);

        var reportId = await reportService.EnqueueForUserAsync(user.Id, "InitialAssessment", assessment.Id, CancellationToken.None);
        await reportService.ProcessJobAsync(ReportJob(reportId), CancellationToken.None);

        var report = await db.EvaluationReports.AsNoTracking().SingleAsync(item => item.Id == reportId);
        Assert.Equal("Ready", report.Status);
        using var doc = JsonDocument.Parse(report.ContentJson);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("schemaVersion").GetInt32());
        var findings = root.GetProperty("findings").EnumerateArray().ToList();
        Assert.Equal(3, findings.Count);
        Assert.All(findings, finding => Assert.False(string.IsNullOrWhiteSpace(finding.GetProperty("statement").GetString())));
        // strengths/weaknesses 由 Finding 派生，兼容旧展示
        Assert.True(root.GetProperty("strengths").GetArrayLength() + root.GetProperty("weaknesses").GetArrayLength() > 0);

        // 无测评关联的报告（挑战触发）回退模板 schemaVersion 1
        var templateReportId = await reportService.EnqueueForUserAsync(user.Id, "ChallengePass", null, CancellationToken.None);
        await reportService.ProcessJobAsync(ReportJob(templateReportId), CancellationToken.None);
        var templateReport = await db.EvaluationReports.AsNoTracking().SingleAsync(item => item.Id == templateReportId);
        using var templateDoc = JsonDocument.Parse(templateReport.ContentJson);
        Assert.Equal(1, templateDoc.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.False(templateDoc.RootElement.TryGetProperty("findings", out _));
    }

    [Fact]
    public async Task Report_falls_back_to_template_when_all_findings_questioned()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-fallback");
        await SeedSentenceLogsAsync(db, user.Id, [4]);
        var assessment = await SeedAssessmentWithFinalAsync(db, user.Id, grammar: 4.0, natural: 4.0, vocabulary: 4.0, relevance: 4.0);

        // LLM 全部产出伪造引用 → 全部存疑 → 报告回退模板
        var llm = new StubProfilerLlm(_ =>
        [
            new ProfileFindingDraft(
                FindingDimension.Skill, "grammar", FindingPolarity.Strength, "伪造结论。",
                [new EvidenceClaim("sentence_log", Guid.NewGuid().ToString(), "grammar", "<=", 4)],
                FindingConfidence.Low)
        ]);
        var reportService = CreateReportService(db, llm);

        var reportId = await reportService.EnqueueForUserAsync(user.Id, "InitialAssessment", assessment.Id, CancellationToken.None);
        await reportService.ProcessJobAsync(ReportJob(reportId), CancellationToken.None);

        var report = await db.EvaluationReports.AsNoTracking().SingleAsync(item => item.Id == reportId);
        Assert.Equal("Ready", report.Status);
        using var doc = JsonDocument.Parse(report.ContentJson);
        Assert.Equal(1, doc.RootElement.GetProperty("schemaVersion").GetInt32());
        // 画像仍然持久化，存疑条目标注留痕（不展示、不进规划）
        var profile = await db.WeaknessProfiles.AsNoTracking().Include(item => item.Findings).SingleAsync(item => item.UserId == user.Id);
        Assert.All(profile.Findings, finding => Assert.Equal(FindingVerification.Questioned, finding.Verification));
    }

    // ── 解析容错 ─────────────────────────────────────────────

    [Fact]
    public void Parser_tolerates_pipe_separated_enum_echoes()
    {
        // qwen 会把提示词里的枚举白名单原样照抄（实测观察）："skill|grammar"、"scenario|skill|reading"
        const string content = """
        {
          "findings": [
            {
              "dimension": "skill|grammar",
              "dimensionKey": "grammar",
              "polarity": "strength|weakness|neutral",
              "statement": "语法稳定。",
              "evidence": [{ "kind": "sentence_log", "refId": "x", "metric": "grammar", "op": "<=", "value": 3 }],
              "confidence": "high|medium|low"
            },
            {
              "dimension": "未知维度",
              "dimensionKey": "grammar",
              "polarity": "strength",
              "statement": "无法识别的维度应被丢弃。",
              "evidence": [],
              "confidence": "low"
            }
          ]
        }
        """;

        var response = LlmResponseParser.ParseWeaknessProfile(content);

        var finding = Assert.Single(response.Findings);
        Assert.Equal(FindingDimension.Skill, finding.Dimension);
        Assert.Equal(FindingPolarity.Strength, finding.Polarity);
        Assert.Equal(FindingConfidence.High, finding.Confidence);
    }

    // ── T-010 画像去重 ─────────────────────────────────────

    [Fact]
    public void Deduplicate_keeps_strongest_finding_per_dimension()
    {
        var weak = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "弱证据条目。",
            [new EvidenceClaim("sentence_log", "a", "grammar", "<=", 2)],
            FindingConfidence.Low);
        var strong = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "强证据条目。",
            [new EvidenceClaim("sentence_log", "b", "grammar", "<=", 2),
             new EvidenceClaim("sentence_log", "c", "grammar", "<=", 2)],
            FindingConfidence.Medium);

        var result = WeaknessProfiler.Deduplicate([weak, strong]);

        var finding = Assert.Single(result);
        Assert.Equal("强证据条目。", finding.Statement);
    }

    [Fact]
    public void Deduplicate_resolves_evidence_reuse_by_confidence()
    {
        var shared = new EvidenceClaim("sentence_log", "shared-id", "grammar", "<=", 2);
        var high = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Weakness, "高置信条目。",
            [shared, new EvidenceClaim("sentence_log", "other-1", "grammar", "<=", 2),
             new EvidenceClaim("sentence_log", "other-2", "grammar", "<=", 2)],
            FindingConfidence.High);
        var low = new ProfileFindingDraft(
            FindingDimension.Scenario, "dining_out", FindingPolarity.Weakness, "低置信复用条目。",
            [shared, new EvidenceClaim("word_stats", "dining_out", "coverage", "<=", 0.3)],
            FindingConfidence.Low);

        var result = WeaknessProfiler.Deduplicate([low, high]);

        Assert.Equal(2, result.Count);
        var highResult = result.Single(item => item.Statement == "高置信条目。");
        Assert.Contains(shared, highResult.Evidence);
        var lowResult = result.Single(item => item.Statement == "低置信复用条目。");
        Assert.DoesNotContain(shared, lowResult.Evidence);
        Assert.Single(lowResult.Evidence);
    }

    [Fact]
    public void Deduplicate_drops_finding_left_without_evidence()
    {
        var shared = new EvidenceClaim("reading_stats", "reading", "avglookupcount", ">=", 3);
        var keeper = new ProfileFindingDraft(
            FindingDimension.Reading, "reading", FindingPolarity.Neutral, "保留条目。",
            [shared], FindingConfidence.Medium);
        var stripped = new ProfileFindingDraft(
            FindingDimension.Skill, "vocabulary", FindingPolarity.Weakness, "全靠复用证据的条目。",
            [shared], FindingConfidence.Low);

        var result = WeaknessProfiler.Deduplicate([stripped, keeper]);

        var finding = Assert.Single(result);
        Assert.Equal("保留条目。", finding.Statement);
    }

    [Fact]
    public async Task Generate_deduplicates_llm_drafts_before_verify()
    {
        await using var db = await PostgresTestDatabase.CreateContextAsync();
        var user = await SeedUserAsync(db, "profile-dedup");
        var logs = await SeedSentenceLogsAsync(db, user.Id, [4, 4, 4]);
        var assessment = await SeedAssessmentWithFinalAsync(db, user.Id, grammar: 4.0, natural: 4.0, vocabulary: 3.0, relevance: 4.0);

        var strong = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Strength, "语法稳定（强证据）。",
            logs.Select(log => new EvidenceClaim("sentence_log", log.Id.ToString(), "grammar", "<=", (double)log.GrammarScore)).ToList(),
            FindingConfidence.High);
        var sameDimension = new ProfileFindingDraft(
            FindingDimension.Skill, "grammar", FindingPolarity.Strength, "同维度重复条目。",
            [new EvidenceClaim("sentence_log", logs[0].Id.ToString(), "grammar", "<=", 4)],
            FindingConfidence.Low);
        var evidenceReuse = new ProfileFindingDraft(
            FindingDimension.Skill, "natural", FindingPolarity.Strength, "复用他人证据的条目。",
            logs.Select(log => new EvidenceClaim("sentence_log", log.Id.ToString(), "grammar", "<=", (double)log.GrammarScore)).ToList(),
            FindingConfidence.Low);

        var llm = new StubProfilerLlm(_ => [strong, sameDimension, evidenceReuse]);
        var service = CreateProfileService(db, llm);

        var profile = await service.GenerateAsync(user.Id, assessment.Id, CancellationToken.None);

        // 同维度去重 + 证据复用剥夺后整条丢弃 → 仅剩强证据条目
        var finding = Assert.Single(profile.Findings);
        Assert.Equal("语法稳定（强证据）。", finding.Statement);
        Assert.Equal(FindingVerification.Verified, finding.Verification);
    }

    // ── 数据播种 ─────────────────────────────────────────────

    private static async Task<User> SeedUserAsync(ApplicationDbContext db, string name)
    {
        var user = new User { DisplayName = $"{name}-{Guid.NewGuid():N}" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<List<SentenceLog>> SeedSentenceLogsAsync(ApplicationDbContext db, Guid userId, int[] grammarScores)
    {
        var logs = grammarScores.Select((grammar, index) => new SentenceLog
        {
            UserId = userId,
            TargetWord = $"word{index}",
            Scene = "assessment",
            UserSentence = $"Sentence {index} for profiling.",
            GrammarScore = grammar,
            NaturalScore = grammar,
            VocabularyScore = grammar,
            RelevanceScore = grammar,
            ErrorTags = grammar <= 2 ? ["verb-form"] : []
        }).ToList();
        db.SentenceLogs.AddRange(logs);
        await db.SaveChangesAsync();
        return logs;
    }

    private static async Task<Assessment> SeedAssessmentWithFinalAsync(
        ApplicationDbContext db, Guid userId, double grammar, double natural, double vocabulary, double relevance)
    {
        var assessment = new Assessment
        {
            UserId = userId,
            Type = AssessmentType.Initial,
            Status = AssessmentStatus.Completed,
            FinalLevel = CefrLevel.B1
        };
        db.Assessments.Add(assessment);
        var final = new AssessmentFinalResult(
            CefrLevel.B1,
            80,
            50,
            50,
            CefrLevel.B1,
            CefrLevel.B1,
            new AssessmentDimensionSummary(grammar, natural, vocabulary, relevance, ["verb-form"], ["comment"]),
            null);
        db.AssessmentRecords.Add(new AssessmentRecord
        {
            AssessmentId = assessment.Id,
            Step = AssessmentStepType.FinalLevel,
            QuestionType = "final",
            QuestionsJson = "{}",
            AnswersJson = "{}",
            ScoresJson = JsonSerializer.Serialize(final, JsonOptions)
        });
        await db.SaveChangesAsync();
        return assessment;
    }

    /// <summary>播种场景词 + 掌握关系 + 阅读留痕；返回 Verifier 同源重算的正确率与平均查词数。</summary>
    private static async Task<(double CorrectRate, double AvgLookup)> SeedStatsAsync(ApplicationDbContext db, Guid userId, string salt)
    {
        var suffix = $"{salt}-{Guid.NewGuid():N}";
        for (var i = 0; i < 2; i++)
        {
            var word = new Word
            {
                Lemma = $"dish{suffix}{i}",
                Meanings = ["菜"],
                CefrLevel = CefrLevel.A2,
                DifficultyLevel = DifficultyLevel.Basic
            };
            word.Scenarios.Add(new WordScenario { WordId = word.Id, ScenarioKey = "dining_out" });
            db.Words.Add(word);
            db.UserWordRelationships.Add(new UserWordRelationship
            {
                UserId = userId,
                WordId = word.Id,
                MasteryScore = 0.5,
                TimesLearned = 4,
                TimesCorrect = 2
            });
        }

        var article = new Article
        {
            Title = $"reading-{suffix}",
            Content = "A short reading article for profiling tests.",
            CefrLevel = CefrLevel.A2,
            WordCount = 30
        };
        db.Articles.Add(article);
        db.ReadingLogs.Add(new ReadingLog { UserId = userId, ArticleId = article.Id, LookupCount = 2 });
        db.ReadingLogs.Add(new ReadingLog { UserId = userId, ArticleId = article.Id, LookupCount = 4 });
        await db.SaveChangesAsync();

        // 与 WeaknessProfileStats 同源口径：(2+2)/(4+4)=0.5；(2+4)/2=3.0
        return (0.5, 3.0);
    }

    /// <summary>构造全部可通过核查的真实草稿：3 条 sentence_log 证据 + 场景统计 + 阅读统计。</summary>
    private static List<ProfileFindingDraft> TruthfulDrafts(List<SentenceLog> logs, double correctRate, double avgLookup)
    {
        return
        [
            new ProfileFindingDraft(
                FindingDimension.Skill, "grammar", FindingPolarity.Strength, "语法维度表现稳定，造句留痕均在高分段。",
                logs.Select(log => new EvidenceClaim("sentence_log", log.Id.ToString(), "grammar", "<=", (double)log.GrammarScore)).ToList(),
                FindingConfidence.High),
            new ProfileFindingDraft(
                FindingDimension.Scenario, "dining_out", FindingPolarity.Weakness, "点餐场景词掌握正确率偏低。",
                [new EvidenceClaim("word_stats", "dining_out", "correctrate", "<=", correctRate)],
                FindingConfidence.Low),
            new ProfileFindingDraft(
                FindingDimension.Reading, "reading", FindingPolarity.Neutral, "阅读查词频率中等。",
                [new EvidenceClaim("reading_stats", "reading", "avglookupcount", ">=", avgLookup)],
                FindingConfidence.Low)
        ];
    }

    // ── 服务装配 ─────────────────────────────────────────────

    private static WeaknessProfileService CreateProfileService(ApplicationDbContext db, ILLMProvider llm)
    {
        var profiler = new WeaknessProfiler(db, new StubLlmFactory(llm));
        return new WeaknessProfileService(db, profiler, new FindingVerifier(db));
    }

    private static EvaluationReportService CreateReportService(ApplicationDbContext db, ILLMProvider llm)
    {
        var scoreProfile = new ScoreProfileService(db, new ScoreMappingService(new ScoreMappingOptions()));
        var assembler = new EvaluationDataAssembler(new StubToolRegistry());
        return new EvaluationReportService(
            db,
            scoreProfile,
            new StubBackgroundJobs(),
            assembler,
            CreateProfileService(db, llm),
            NullLogger<EvaluationReportService>.Instance);
    }

    private static BackgroundJob ReportJob(long reportId) => new()
    {
        JobType = "EvaluationReport",
        PayloadJson = JsonSerializer.Serialize(new { reportId }, JsonOptions),
        Status = "Processing",
        IdempotencyKey = $"test:{Guid.NewGuid():N}"
    };

    private sealed class StubProfilerLlm(Func<WeaknessProfileRequest, IReadOnlyList<ProfileFindingDraft>> drafts) : ILLMProvider
    {
        public Task<WeaknessProfileResponse> GenerateWeaknessProfileAsync(WeaknessProfileRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new WeaknessProfileResponse(drafts(request).ToList()));

        public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ScenarioAnnotationResponse> AnnotateScenarioAsync(ScenarioAnnotationRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class StubLlmFactory(ILLMProvider provider) : IUserLlmProviderFactory
    {
        public Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(provider);
    }

    private sealed class StubToolRegistry : ILearningToolRegistry
    {
        public IReadOnlyList<string> ToolNames => [];
        public Task<object> InvokeAsync(string toolName, JsonElement args, Guid userId, CancellationToken cancellationToken)
            => Task.FromResult<object>(new List<object>());
    }

    private sealed class StubBackgroundJobs : IBackgroundJobService
    {
        public Task<long> EnqueueAsync(string jobType, string payloadJson, string idempotencyKey, CancellationToken cancellationToken) => Task.FromResult(1L);
        public Task ProcessPendingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
