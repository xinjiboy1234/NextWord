namespace NextWord.Domain.Services;

/// <summary>
/// 阅读辅助 Agent 可调用的 skills 元数据注册表。
/// Agent 通过名称查找 skill，不直接耦合具体实现。
/// </summary>
public static class ReadingSkillRegistry
{
    public const string LookupWord = "lookup_word";
    public const string ExplainInContext = "explain_in_context";
    public const string ExtractKeyVocab = "extract_key_vocab";
    public const string GenerateExamples = "generate_examples";
    public const string CommentReply = "comment_reply";

    public static IReadOnlyList<string> All { get; } =
    [
        LookupWord,
        ExplainInContext,
        ExtractKeyVocab,
        GenerateExamples,
        CommentReply
    ];
}
