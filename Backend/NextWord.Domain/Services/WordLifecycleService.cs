using NextWord.Domain.Entities;
using NextWord.Domain.Enums;

namespace NextWord.Domain.Services;

/// <summary>
/// T-014 词毕业四阶段生命周期（DESIGN-word-lifecycle §2/§3）：
/// 纯规则状态机，掌握度由阶段派生；自评（Remembered/Forgot）只改 SM-2 排程参数，
/// 不参与掌握度与 Score 计算——本类只读 SM-2 已更新的 RepeatCount 判成熟，绝不按自评加减掌握度。
/// </summary>
public static class WordLifecycleService
{
    /// <summary>SM-2 成熟阈值：连续 Remembered 次数（Forgot 会清零 RepeatCount，天然连续口径；复用现有口径不新造指标）。</summary>
    public const int MatureRepeatCount = 2;

    /// <summary>掌握度阶段派生：认识/回忆只算「认识」，造句使用算「会用」，自发使用才算「毕业」。</summary>
    public static double MasteryForStage(WordLifecycleStage stage) => stage switch
    {
        WordLifecycleStage.Recognized => 25,
        WordLifecycleStage.Recalled => 50,
        WordLifecycleStage.PromptedUse => 75,
        WordLifecycleStage.SpontaneousUse => 100,
        _ => 0
    };

    /// <summary>对外 token（接口/前端口径，小写下划线）。</summary>
    public static string ToToken(WordLifecycleStage stage) => stage switch
    {
        WordLifecycleStage.Recognized => "recognized",
        WordLifecycleStage.Recalled => "recalled",
        WordLifecycleStage.PromptedUse => "prompted_use",
        WordLifecycleStage.SpontaneousUse => "spontaneous_use",
        _ => "recognized"
    };

    public static WordLifecycleStage ParseToken(string? token) => token?.Trim().ToLowerInvariant() switch
    {
        "recalled" => WordLifecycleStage.Recalled,
        "prompted_use" => WordLifecycleStage.PromptedUse,
        "spontaneous_use" => WordLifecycleStage.SpontaneousUse,
        _ => WordLifecycleStage.Recognized
    };

    public static WordQuizMode ParseQuizMode(string? mode) =>
        string.Equals(mode?.Trim(), "recall", StringComparison.OrdinalIgnoreCase)
            ? WordQuizMode.Recall
            : WordQuizMode.Recognition;

    public static string QuizModeToken(WordQuizMode mode) =>
        mode == WordQuizMode.Recall ? "recall" : "recognition";

    /// <summary>背词考察模式按阶段切换：认识=看词知义，回忆及以后=看义想词。</summary>
    public static WordQuizMode QuizModeForStage(WordLifecycleStage stage) =>
        stage == WordLifecycleStage.Recognized ? WordQuizMode.Recognition : WordQuizMode.Recall;

    /// <summary>
    /// 背词提交后调用（先 <see cref="ISm2Service.ApplyReview"/> 再调本方法）：
    /// 认识→回忆：看词知义连续正确达到 SM-2 成熟阈值；回忆→造句使用：回忆模式考察通过（进产出候选池）。
    /// 认识/回忆阶段不回退（SM-2 管遗忘调度）。掌握度始终按阶段重算。
    /// </summary>
    public static void ApplyReview(
        UserWordRelationship relationship, WordQuizMode mode, bool isCorrect, DateTimeOffset now)
    {
        if (isCorrect)
        {
            if (mode == WordQuizMode.Recognition
                && relationship.LifecycleStage == WordLifecycleStage.Recognized
                && relationship.RepeatCount >= MatureRepeatCount)
            {
                SetStage(relationship, WordLifecycleStage.Recalled, now);
            }
            else if (mode == WordQuizMode.Recall
                && relationship.LifecycleStage == WordLifecycleStage.Recalled)
            {
                SetStage(relationship, WordLifecycleStage.PromptedUse, now);
            }
        }

        relationship.MasteryScore = MasteryForStage(relationship.LifecycleStage);
    }

    /// <summary>产出评分达标口径：OverallGrade A/B（与瓶颈筛查的四维口径分开，规则引擎拍板）。</summary>
    public static bool IsPassingGrade(string? overallGrade) =>
        string.Equals(overallGrade?.Trim(), "A", StringComparison.OrdinalIgnoreCase)
        || string.Equals(overallGrade?.Trim(), "B", StringComparison.OrdinalIgnoreCase);

    /// <summary>提示造句正确使用：句中含目标词（词边界命中）且句子达标。</summary>
    public static bool IsPromptedUseCorrect(IReadOnlySet<string> tokens, string lemma, string? overallGrade) =>
        tokens.Contains(lemma.Trim().ToLowerInvariant()) && IsPassingGrade(overallGrade);

    /// <summary>提示造句使用错误（回退信号）：句中含目标词但用错——D 档或词汇维低分；不含目标词算回避（安全词信号另管），不算使用错误。</summary>
    public static bool IsPromptedUseMisuse(IReadOnlySet<string> tokens, string lemma, string? overallGrade, int vocabularyScore) =>
        tokens.Contains(lemma.Trim().ToLowerInvariant())
        && (string.Equals(overallGrade?.Trim(), "D", StringComparison.OrdinalIgnoreCase) || vocabularyScore <= 2);

    /// <summary>提示造句正确使用确认（造句使用 → 待自发；confirmed 后 Planner 不再重复编排该词）。</summary>
    public static void ConfirmPromptedUse(UserWordRelationship relationship, DateTimeOffset now)
    {
        if (relationship.LifecycleStage == WordLifecycleStage.PromptedUse
            && relationship.PromptedUseConfirmedAt is null)
        {
            relationship.PromptedUseConfirmedAt = now;
        }
    }

    /// <summary>产出证据显示不会用：退回回忆阶段重进 SM-2 调度（RepeatCount/Interval 归零重来）。</summary>
    public static void RegressToRecall(UserWordRelationship relationship, DateTimeOffset now)
    {
        if (relationship.LifecycleStage != WordLifecycleStage.PromptedUse)
        {
            return;
        }

        relationship.PromptedUseConfirmedAt = null;
        relationship.RepeatCount = 0;
        relationship.IntervalDays = 1;
        relationship.NextReviewDue = now;
        SetStage(relationship, WordLifecycleStage.Recalled, now);
    }

    /// <summary>自发使用毕业：自由表达中自发正确使用一次且当次评分达标；留痕所在 FreeExpressionLog。</summary>
    public static void Graduate(UserWordRelationship relationship, Guid freeExpressionLogId, DateTimeOffset now)
    {
        if (relationship.LifecycleStage != WordLifecycleStage.PromptedUse)
        {
            return;
        }

        relationship.GraduatedFreeExpressionLogId = freeExpressionLogId;
        SetStage(relationship, WordLifecycleStage.SpontaneousUse, now);
    }

    private static void SetStage(UserWordRelationship relationship, WordLifecycleStage stage, DateTimeOffset now)
    {
        relationship.LifecycleStage = stage;
        relationship.StageUpdatedAt = now;
        relationship.MasteryScore = MasteryForStage(stage);
    }
}
