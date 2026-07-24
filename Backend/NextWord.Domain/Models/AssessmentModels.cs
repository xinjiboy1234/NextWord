using NextWord.Domain.Enums;

namespace NextWord.Domain.Models;

public sealed record VocabQuizQuestion(string Word, IReadOnlyList<string> Options, int CorrectIndex, DifficultyLevel Difficulty);
public sealed record SpellingQuizQuestion(string Chinese, string CorrectSpelling, DifficultyLevel Difficulty);
public sealed record SentenceQuizQuestion(Guid? WordId, string Word, string Scene);
public sealed record ReadingQuizQuestion(Guid ArticleId, string Question, IReadOnlyList<string> Options, int CorrectIndex, string ArticleExcerpt);

public sealed record ChallengePack(
    IReadOnlyList<VocabQuizQuestion> Vocabulary,
    SentenceQuizQuestion Sentence,
    ReadingQuizQuestion Reading,
    CefrLevel AttemptedLevel);

// ── 自适应分块测评（T-004，DESIGN-assessment-rework）────────────────────

/// <summary>「取下一块」响应：未收敛时带 Block，收敛后带 Final。</summary>
public sealed record AssessmentBlockResponse(bool Converged, AssessmentBlockView? Block, AssessmentFinalResult? Final);

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
/// </summary>
public sealed record AssessmentFinalResult(
    CefrLevel OverallLevel,
    int ExpressionScore,
    int VocabularyReferenceScore,
    int ReadingReferenceScore,
    CefrLevel VocabularyReferenceLevel,
    CefrLevel ReadingReferenceLevel,
    AssessmentDimensionSummary Dimensions,
    long? EvaluationReportId);

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
