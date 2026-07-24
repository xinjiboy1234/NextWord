namespace NextWord.Domain.Enums;

/// <summary>
/// 表达角色（设计方案 §3）：核心动词 / 连接过渡 / 场景名词 / 句型短语。
/// </summary>
public enum ExpressionRole
{
    CoreVerb = 1,
    Connector = 2,
    SceneNoun = 3,
    PhrasePattern = 4
}
