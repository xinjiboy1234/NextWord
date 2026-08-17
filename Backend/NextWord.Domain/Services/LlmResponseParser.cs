using NextWord.Domain.Enums;
using NextWord.Domain.Models;
using System.Text.Json;

namespace NextWord.Domain.Services;

public static class LlmResponseParser
{
    public static DifficultyRating EnsureValid(DifficultyRating rating)
    {
        if (rating.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("LLM confidence must be between 0 and 1.");
        }

        return rating;
    }

    /// <summary>
    /// T-061：解析真实 LLM 的结构化难度标注（BuildDifficultyPrompt 的 JSON 输出）。
    /// difficulty/cefr 无法识别时回退默认值；intrinsic_score 缺省为空（调用方回退 CEFR 映射）。
    /// </summary>
    public static DifficultyRating ParseDifficulty(string content, ItemType itemType, string modelProfileId = "difficulty-v1")
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<DifficultyRatingJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty difficulty rating.");
        var difficulty = ParseDifficultyLevel(parsed.DifficultyLevel);
        var cefr = ParseCefr(parsed.CefrLevel);
        var intrinsic = parsed.IntrinsicScore is int score
            ? Math.Clamp(score, 0, 100)
            : (int?)LegacyCefrIntrinsic(cefr);
        return new DifficultyRating(
            itemType,
            difficulty,
            cefr,
            string.IsNullOrWhiteSpace(parsed.Reason) ? "LLM difficulty annotation." : parsed.Reason.Trim(),
            RecommendedAction.ReviewLater,
            Math.Clamp(parsed.Confidence, 0, 1),
            modelProfileId,
            intrinsic);
    }

    private static DifficultyLevel ParseDifficultyLevel(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "basic" => DifficultyLevel.Basic,
            "intermediate" => DifficultyLevel.Intermediate,
            "advanced" => DifficultyLevel.Advanced,
            _ => DifficultyLevel.Intermediate
        };

    private static CefrLevel ParseCefr(string value) =>
        Enum.TryParse<CefrLevel>(value.Trim(), ignoreCase: true, out var level) ? level : CefrLevel.B1;

    /// <summary>T-061：CEFR 档到 0–100 内在难度分的兜底映射（与 LegacyScoreHelper.FromCefr 同口径，Domain 侧复制避免依赖 Infrastructure）。</summary>
    private static int LegacyCefrIntrinsic(CefrLevel level) => level switch
    {
        CefrLevel.A1 => 10,
        CefrLevel.A2 => 27,
        CefrLevel.B1 => 52,
        CefrLevel.B2 => 77,
        CefrLevel.C1 => 90,
        _ => 97
    };

    /// <summary>真实 LLM 难度标注 JSON 载荷（BuildDifficultyPrompt 契约）。</summary>
    private sealed class DifficultyRatingJson
    {
        public string DifficultyLevel { get; set; } = "intermediate";
        public string CefrLevel { get; set; } = "B1";
        public int? IntrinsicScore { get; set; }
        public string? Reason { get; set; }
        public double Confidence { get; set; } = 0.5;
    }

    public static SentenceRatingResponse ParseSentenceRating(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<SentenceRatingJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty sentence rating.");
        return parsed.ToResponse();
    }

    public static DefinitionResponse ParseDefinition(string content, string word, string? context)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<DefinitionJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty word definition.");
        return parsed.ToResponse(word, context);
    }

    public static VocabExtractResponse ParseVocabExtract(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<VocabExtractJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty vocab extraction.");
        return new VocabExtractResponse(
            parsed.KeyVocab.Select(item => new KeyVocabItem(
                item.Word,
                item.Phonetics,
                item.ContextMeaning,
                MapOptionalExample(item.UsageExample, WordExampleKind.Contextual),
                MapOptionalExample(item.GeneralExample, WordExampleKind.General),
                ParseDifficulty(item.Difficulty),
                ParseAction(item.Action))).ToList(),
            parsed.SkippedBasic,
            parsed.SkippedRare);
    }

    /// <summary>
    /// 解析场景标注结果：无效场景 key 丢弃、场景数截断到 3 个、utility/role 无法识别时丢弃该条（由调用方决定重试）。
    /// </summary>
    public static ScenarioAnnotationResponse ParseScenarioAnnotation(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<ScenarioAnnotationJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty scenario annotation.");

        var results = new List<ScenarioAnnotationResult>();
        foreach (var item in parsed.Annotations)
        {
            if (string.IsNullOrWhiteSpace(item.Lemma)
                || !TryParseUtility(item.Utility, out var utility)
                || !TryParseRole(item.Role, out var role))
            {
                continue;
            }

            var scenarioKeys = item.Scenarios
                .Where(Scenarios.ScenarioTaxonomy.IsSubScenarioKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToList();

            results.Add(new ScenarioAnnotationResult(item.Lemma.Trim().ToLowerInvariant(), scenarioKeys, utility, role));
        }

        return new ScenarioAnnotationResponse(results);
    }

    private static bool TryParseUtility(string value, out WordUtility utility)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "high": utility = WordUtility.High; return true;
            case "medium": utility = WordUtility.Medium; return true;
            case "low": utility = WordUtility.Low; return true;
            default: utility = default; return false;
        }
    }

    private static bool TryParseRole(string value, out ExpressionRole role)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "core_verb": role = ExpressionRole.CoreVerb; return true;
            case "connector": role = ExpressionRole.Connector; return true;
            case "scene_noun": role = ExpressionRole.SceneNoun; return true;
            case "phrase_pattern": role = ExpressionRole.PhrasePattern; return true;
            default: role = default; return false;
        }
    }

    /// <summary>
    /// 解析 Profiler 产出的 Finding 列表（T-005）：dimension/polarity/confidence 无法识别的条目直接丢弃；
    /// 证据引用与数值的真实性不在此判断，由 Verifier 机械核查。
    /// </summary>
    public static WeaknessProfileResponse ParseWeaknessProfile(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<WeaknessProfileJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty weakness profile.");

        var drafts = new List<ProfileFindingDraft>();
        foreach (var item in parsed.Findings)
        {
            if (!TryParseDimension(item.Dimension, out var dimension)
                || !TryParsePolarity(item.Polarity, out var polarity)
                || !TryParseConfidence(item.Confidence, out var confidence))
            {
                continue;
            }

            var evidence = item.Evidence
                .Where(e => !string.IsNullOrWhiteSpace(e.Kind) && !string.IsNullOrWhiteSpace(e.RefId))
                .Select(e => new EvidenceClaim(
                    e.Kind.Trim().ToLowerInvariant(),
                    e.RefId.Trim(),
                    string.IsNullOrWhiteSpace(e.Metric) ? null : e.Metric.Trim().ToLowerInvariant(),
                    string.IsNullOrWhiteSpace(e.Op) ? null : e.Op.Trim(),
                    e.Value))
                .ToList();

            drafts.Add(new ProfileFindingDraft(
                dimension,
                item.DimensionKey.Trim(),
                polarity,
                item.Statement.Trim(),
                evidence,
                confidence));
        }

        return new WeaknessProfileResponse(drafts);
    }

    private static bool TryParseDimension(string value, out FindingDimension dimension)
    {
        foreach (var token in EnumTokens(value))
        {
            switch (token)
            {
                case "scenario": dimension = FindingDimension.Scenario; return true;
                case "skill": dimension = FindingDimension.Skill; return true;
                case "reading": dimension = FindingDimension.Reading; return true;
            }
        }

        dimension = default;
        return false;
    }

    private static bool TryParsePolarity(string value, out FindingPolarity polarity)
    {
        foreach (var token in EnumTokens(value))
        {
            switch (token)
            {
                case "strength": polarity = FindingPolarity.Strength; return true;
                case "weakness": polarity = FindingPolarity.Weakness; return true;
                case "neutral": polarity = FindingPolarity.Neutral; return true;
            }
        }

        polarity = default;
        return false;
    }

    private static bool TryParseConfidence(string value, out FindingConfidence confidence)
    {
        foreach (var token in EnumTokens(value))
        {
            switch (token)
            {
                case "high": confidence = FindingConfidence.High; return true;
                case "medium": confidence = FindingConfidence.Medium; return true;
                case "low": confidence = FindingConfidence.Low; return true;
            }
        }

        confidence = default;
        return false;
    }

    /// <summary>
    /// 解析 InsightAgent 产出的瓶颈洞察（T-007）：nature 无法识别视为整条失败（由调用方回退）；
    /// 证据 id 的真实性不在此判断，持久化前由服务层对照真实 SentenceLog 机械过滤。
    /// </summary>
    public static BottleneckInsightResponse ParseBottleneckInsight(string content)
    {
        var json = ExtractJson(content);
        var parsed = JsonSerializer.Deserialize<BottleneckInsightJson>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("LLM returned empty bottleneck insight.");
        if (!TryParseNature(parsed.Nature, out var nature))
        {
            throw new InvalidOperationException($"LLM returned unrecognized bottleneck nature: {parsed.Nature}");
        }

        var evidenceIds = parsed.EvidenceLogIds
            .Select(raw => Guid.TryParse(raw, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        return new BottleneckInsightResponse(nature, parsed.Statement.Trim(), evidenceIds);
    }

    private static bool TryParseNature(string value, out BottleneckNature nature)
    {
        foreach (var token in EnumTokens(value))
        {
            switch (token)
            {
                case "vocabulary_insufficient": nature = BottleneckNature.VocabularyInsufficient; return true;
                case "cannot_organize_sentences": nature = BottleneckNature.CannotOrganizeSentences; return true;
                case "grammar_errors": nature = BottleneckNature.GrammarErrors; return true;
                case "monotonous_expression": nature = BottleneckNature.MonotonousExpression; return true;
                case "avoidance_pattern": nature = BottleneckNature.AvoidancePattern; return true;
                case "chinglish_collocation": nature = BottleneckNature.ChinglishCollocation; return true;
                case "safe_word_strategy": nature = BottleneckNature.SafeWordStrategy; return true;
            }
        }

        nature = default;
        return false;
    }

    /// <summary>qwen 等模型可能把枚举白名单原样照抄（"skill|grammar"、"scenario|skill|reading"）：逐 token 取第一个可识别值。</summary>
    private static IEnumerable<string> EnumTokens(string value) =>
        value.Trim().ToLowerInvariant().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static WordExample? MapOptionalExample(WordExampleJsonDto? item, WordExampleKind kind)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Sentence))
        {
            return null;
        }

        return new WordExample(
            kind,
            item.Sentence.Trim(),
            string.IsNullOrWhiteSpace(item.Explanation) ? item.Sentence.Trim() : item.Explanation.Trim());
    }

    private static DifficultyLevel ParseDifficulty(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "intermediate" => DifficultyLevel.Intermediate,
            "advanced" => DifficultyLevel.Advanced,
            _ => DifficultyLevel.Basic
        };

    private static RecommendedAction ParseAction(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "review_later" => RecommendedAction.ReviewLater,
            "challenge_only" => RecommendedAction.ChallengeOnly,
            _ => RecommendedAction.LearnNow
        };

    private static string ExtractJson(string content)
    {
        var trimmed = content.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new InvalidOperationException("LLM response did not contain a JSON object.");
        }

        return trimmed[start..(end + 1)];
    }
}
