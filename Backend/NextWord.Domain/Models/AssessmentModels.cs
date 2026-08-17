using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

public sealed record VocabQuizQuestion(string Word, IReadOnlyList<string> Options, int CorrectIndex, DifficultyLevel Difficulty);
public sealed record SpellingQuizQuestion(string Chinese, string CorrectSpelling, DifficultyLevel Difficulty);
public sealed record SentenceQuizQuestion(Guid? WordId, string Word, string Scene);
public sealed record ReadingQuizQuestion(Guid ArticleId, string Question, IReadOnlyList<string> Options, int CorrectIndex, string ArticleExcerpt);

/// <summary>
/// 挑战包。T-035 起阅读从 1 题增至 3 题：<see cref="Readings"/> 为完整题组，
/// <see cref="Reading"/> 保留为第一题，兼容旧会话 JSON（无 readings 属性时 Readings 为 null，回退单题）。
/// </summary>
public sealed record ChallengePack(
    IReadOnlyList<VocabQuizQuestion> Vocabulary,
    SentenceQuizQuestion Sentence,
    ReadingQuizQuestion Reading,
    CefrLevel AttemptedLevel)
{
    /// <summary>阅读题组（T-035，3 题）；旧会话数据无此属性，反序列化为 null。</summary>
    public IReadOnlyList<ReadingQuizQuestion>? Readings { get; init; }
}

// ── 自适应分块测评（T-004，DESIGN-assessment-rework）────────────────────

/// <summary>「取下一块」响应：未收敛时带 Block，收敛后带 Final。</summary>
/// <summary>T-065：Evaluating=true 表示本块已提交答案、后台评分中（前端轮询该标记直到出题/收敛）。</summary>
public sealed record AssessmentBlockResponse(bool Converged, AssessmentBlockView? Block, AssessmentFinalResult? Final, bool Evaluating = false);

/// <summary>客户端块视图（不含正确答案）。产出题 ≥60%，识别题仅作参考。</summary>
public sealed record AssessmentBlockView(
    int BlockIndex,
    int MaxBlocks,
    CefrLevel Band,
    IReadOnlyList<AssessmentProductionPrompt> Production,
    IReadOnlyList<AssessmentVocabChoice> Vocabulary,
    AssessmentReadingItem? Reading);

/// <summary>产出题提示：Kind = sentence（提示造句）| scenario（情境表达）。</summary>
public sealed record AssessmentProductionPrompt(string Id, string Kind, string? TargetWord, string ScenarioZh, string Prompt);

/// <summary>词义选择（识别型参考题）。</summary>
public sealed record AssessmentVocabChoice(string Id, string Word, IReadOnlyList<string> Options);

/// <summary>阅读理解（识别型参考题），文章来自库内并按难度带选文。</summary>
public sealed record AssessmentReadingItem(string Id, string Title, string Content, string Question, IReadOnlyList<string> Options);

/// <summary>作答项：产出题给 Text，识别题给 SelectedIndex。</summary>
public sealed record AssessmentAnswerItem(string Id, string? Text, int? SelectedIndex, int? LookupCount);

public sealed record AssessmentBlockResult(
    bool Converged,
    int BlockIndex,
    CefrLevel Band,
    CefrLevel? NextBand,
    double BlockExpressionScore,
    AssessmentFinalResult? Final);

/// <summary>
/// 定级结果：主叙事为表达力综合分（产出维度加权）；识别分仅参考展示，不参与主定级。
/// 固定等级外壳保留；Dimensions 为可读维度评价内核（供 T-005 WeaknessProfile）。
/// OriginalLevelBeforeGuard：T-042 识别防伪闸留痕——发生矫正时为表达定级原档，未矫正为 null。
/// Rubric：T-055 人话水平标签（DESIGN-assessment-visibility §3.1）——新测评起随结果持久化，旧记录 JSON 无此字段为 null，前端降级不显示。
/// </summary>
public sealed record AssessmentFinalResult(
    CefrLevel OverallLevel,
    int ExpressionScore,
    int VocabularyReferenceScore,
    int ReadingReferenceScore,
    CefrLevel VocabularyReferenceLevel,
    CefrLevel ReadingReferenceLevel,
    AssessmentDimensionSummary Dimensions,
    long? EvaluationReportId,
    CefrLevel? OriginalLevelBeforeGuard = null,
    ProficiencyRubricView? Rubric = null);

/// <summary>T-055 人话 rubric 视图：总体标签 + 四维（中文名、得分、特征描述），全部为用户可读中文文案。</summary>
public sealed record ProficiencyRubricView(
    string OverallLabel,
    string OverallDescription,
    IReadOnlyList<RubricDimensionView> Dimensions);

/// <summary>T-055 单个维度的人话描述（Name 为中文维度名）。</summary>
public sealed record RubricDimensionView(string Name, double Score, string Description);

/// <summary>
/// T-054 测评历史列表项（GET /api/assessments）：ExpressionScore 与 GuardAdjusted
/// 从 FinalLevel 记录的 AssessmentFinalResult JSON 投影；进行中的测评两者为 null/false。
/// </summary>
public sealed record AssessmentListItem(
    Guid Id,
    AssessmentType Type,
    AssessmentStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    CefrLevel? FinalLevel,
    int? ExpressionScore,
    bool GuardAdjusted);

/// <summary>
/// T-054 测评详情视图（GET /api/assessment/{id}）：Assessment + Records 投影，不含导航回引用——
/// 直接返回实体会因 AssessmentRecord.Assessment 循环引用导致序列化失败（qa-t054 D1）。
/// </summary>
public sealed record AssessmentDetailView(
    Guid Id,
    Guid UserId,
    AssessmentType Type,
    AssessmentStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    CefrLevel? FinalLevel,
    IReadOnlyList<AssessmentRecordView> Records)
{
    public static AssessmentDetailView FromEntity(Assessment assessment) => new(
        assessment.Id,
        assessment.UserId,
        assessment.Type,
        assessment.Status,
        assessment.StartAt,
        assessment.EndAt,
        assessment.FinalLevel,
        assessment.Records
            .OrderBy(record => record.Timestamp)
            .Select(AssessmentRecordView.FromEntity)
            .ToList());
}

/// <summary>详情内的记录视图：题目/作答/评分以 JSON 字符串原样透出，前端按 Step 解析。</summary>
public sealed record AssessmentRecordView(
    Guid Id,
    AssessmentStepType Step,
    string QuestionType,
    string QuestionsJson,
    string AnswersJson,
    string ScoresJson,
    DateTimeOffset Timestamp)
{
    public static AssessmentRecordView FromEntity(AssessmentRecord record) => new(
        record.Id,
        record.Step,
        record.QuestionType,
        record.QuestionsJson,
        record.AnswersJson,
        record.ScoresJson,
        record.Timestamp);
}

public sealed record AssessmentDimensionSummary(
    double Grammar,
    double Natural,
    double Vocabulary,
    double Relevance,
    IReadOnlyList<string> TopErrorTags,
    IReadOnlyList<string> Comments);

public sealed record UpgradeCheckResult(
    bool IsCandidate,
    bool RequiresConfirmationChallenge,
    string Summary);
