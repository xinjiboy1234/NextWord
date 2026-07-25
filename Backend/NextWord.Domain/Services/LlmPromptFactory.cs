using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public static class LlmPromptFactory
{
    public static string BuildDifficultyPrompt(ItemRatingRequest request)
    {
        return $"Rate the {request.ItemType} difficulty for: {request.Text}";
    }

    public static string BuildSentenceRatingPrompt(SentenceRatingRequest request)
    {
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);

        return $$"""
        You are an English language assessment assistant. Rate this sentence.

        User Level: {{request.UserLevel}}
        Target Word: {{request.TargetWord}}
        Scene: {{request.Scene}}
        User Sentence: {{request.UserSentence}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

        Return only JSON:
        {
          "grammar_score": 0,
          "natural_score": 0,
          "vocabulary_score": 0,
          "relevance_score": 0,
          "overall_grade": "A/B/C/D",
          "ai_revision": "string",
          "error_analysis": ["string"],
          "difficulty_level": "basic|intermediate|advanced",
          "suggestion": "string"
        }

        Rules:
        - Scores must be integers from 0 to 5.
        - Be fair but not overly generous.
        - Evaluate whether the target word is used naturally and correctly.
        - If this is free expression, evaluate the whole passage.
        - Write error_analysis and suggestion in {{explanationLanguageName}}.
        - Keep ai_revision in natural English as the corrected learner sentence.
        """;
    }

    public static string BuildDefinitionPrompt(DefinitionRequest request)
    {
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);
        var context = string.IsNullOrWhiteSpace(request.Context) ? "(no sentence provided)" : request.Context;

        return $$"""
        You are a vocabulary assistant for English learners. Explain a word in reading context.

        Word: {{request.Word}}
        Context Sentence: {{context}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

        Return only JSON:
        {
          "phonetics": "string",
          "meanings": [
            {
              "definition": "string",
              "is_contextual": true
            }
          ],
          "collocations": ["string"],
          "examples": [
            {
              "kind": "contextual",
              "sentence": "English sentence tied to the context",
              "explanation": "essence note in {{explanationLanguageName}}"
            },
            {
              "kind": "general",
              "sentence": "English sentence from another scenario",
              "explanation": "essence note in {{explanationLanguageName}}"
            }
          ],
          "special_usage": "string",
          "difficulty_level": "basic|intermediate|advanced",
          "cefr_level": "A1|A2|B1|B2|C1|C2"
        }

        Rules:
        - meanings[0] must explain how the word is used in the given context sentence.
        - Write definition, special_usage, collocation glosses, and example explanations in {{explanationLanguageName}}.
        - Keep example sentences in natural English.
        - examples[0] (contextual) must reflect usage in the given context sentence.
        - examples[1] (general) should come from a different everyday scenario; omit if the word is too rare, too specialized, or not worth illustrating at this level.
        - Return 0-2 examples. Be concise: one primary contextual meaning, up to 2 collocations.
        """;
    }

    public static string BuildVocabExtractPrompt(VocabExtractRequest request)
    {
        var explanationLanguage = ExplanationLanguageHelper.Resolve(
            request.ExplanationLanguage,
            ExplanationLanguageHelper.Default);
        var explanationLanguageName = ExplanationLanguageHelper.GetPromptDisplayName(explanationLanguage);

        return $$"""
        You are a vocabulary extraction assistant for English learners.

        Article Level: {{request.ArticleLevel}}
        User Level: {{request.UserLevel}}
        Feedback Language: {{explanationLanguage}} ({{explanationLanguageName}})

        Article Title: {{request.ArticleTitle}}

        Article:
        {{request.ArticleContent}}

        Extract vocabulary worth learning for a {{request.UserLevel}} student.
        Return only JSON:
        {
          "keyVocab": [
            {
              "word": "string",
              "phonetics": "string",
              "contextMeaning": "string",
              "usageExample": {
                "sentence": "English sentence from this article's context",
                "explanation": "essence note in {{explanationLanguageName}}"
              },
              "generalExample": {
                "sentence": "English sentence from another scenario",
                "explanation": "essence note in {{explanationLanguageName}}"
              },
              "difficulty": "basic|intermediate|advanced",
              "action": "learn_now|review_later|challenge_only"
            }
          ],
          "skippedBasic": ["string"],
          "skippedRare": ["string"]
        }

        Rules:
        - Max 10 key vocabulary items.
        - Evaluate each word IN CONTEXT.
        - Do not include very basic words.
        - Keep word as the English lemma.
        - Write contextMeaning and example explanations in {{explanationLanguageName}}.
        - phonetics is required for common words (IPA).
        - usageExample must show how the word is used in THIS article; generalExample is optional for another scenario.
        - Omit generalExample if the word is too specialized or not worth a second example at this level.
        """;
    }

    public static string BuildCommentReplyPrompt(CommentReplyRequest request)
    {
        return $$"""
        You are a reading tutor. Reply helpfully to the learner's paragraph comment.

        Article: {{request.ArticleTitle}}
        Paragraph: {{request.ParagraphText}}
        Learner Comment: {{request.CommentText}}

        Return plain text only, 2-4 sentences, encouraging and explanatory.
        """;
    }

    public static string BuildScenarioAnnotationPrompt(ScenarioAnnotationRequest request)
    {
        var scenarioList = string.Join("\n", Scenarios.ScenarioTaxonomy.All.Select(
            item => $"- {item.Key} ({item.ZhName}, 大类 {item.CategoryKey})"));
        var wordList = string.Join("\n", request.Words.Select(
            item => $"- {item.Lemma} ({item.PartOfSpeech}) {string.Join("；", item.Meanings)}"));

        return $$"""
        You annotate English vocabulary for a life-expression learning app.

        Sub-scenarios (pick 0-3 per word; pick 0 only for cross-scenario core words like be/have/get or connectors):
        {{scenarioList}}

        Fields per word:
        - scenarios: 0-3 sub-scenario keys where the word is most useful for EXPRESSION.
        - utility: high|medium|low — everyday spoken frequency × irreplaceability in expression. Use low for rare/bookish words.
        - role: core_verb|connector|scene_noun|phrase_pattern — the word's role in spoken expression.

        Words:
        {{wordList}}

        Return only JSON:
        {
          "annotations": [
            { "lemma": "string", "scenarios": ["key"], "utility": "high", "role": "core_verb" }
          ]
        }

        Rules:
        - Return exactly one annotation per input word, keeping the input lemma unchanged.
        - scenarios must only use keys from the list above.
        """;
    }

    /// <summary>
    /// Profiler Agent 提示词（T-005）：只给库内聚合好的真实数据，要求 Finding 引用其中的记录与数值，
    /// 供 Verifier 机械回溯核查（防幻觉靠核查，不靠 LLM 自觉）。
    /// </summary>
    public static string BuildWeaknessProfilePrompt(WeaknessProfileRequest request)
    {
        var logLines = request.SentenceLogs.Count == 0
            ? "(none)"
            : string.Join("\n", request.SentenceLogs.Select(log =>
                $"- id={log.Id} word={log.TargetWord} scene={log.Scene} grammar={log.Grammar} natural={log.Natural} vocabulary={log.Vocabulary} relevance={log.Relevance} errorTags=[{string.Join(",", log.ErrorTags)}]"));

        var dimensions = request.AssessmentDimensions is null
            ? "(none)"
            : $"grammar={request.AssessmentDimensions.Grammar} natural={request.AssessmentDimensions.Natural} vocabulary={request.AssessmentDimensions.Vocabulary} relevance={request.AssessmentDimensions.Relevance} expressionScore={request.ExpressionScore} topErrorTags=[{string.Join(",", request.AssessmentDimensions.TopErrorTags)}]";

        var scenarioLines = request.ScenarioStats.Count == 0
            ? "(none)"
            : string.Join("\n", request.ScenarioStats.Select(stat =>
                $"- scenario={stat.ScenarioKey} ({stat.ScenarioZh}) annotated={stat.AnnotatedWords} learned={stat.LearnedWords} coverage={stat.Coverage} avgMastery={stat.AvgMastery} correctRate={stat.CorrectRate}"));

        return $$"""
        You are the Profiler agent of an English learning app. Write a weakness/strength profile as structured findings, citing ONLY the data below.

        User level: {{request.UserLevel}}

        Assessment dimension averages (0-5 per dimension, expressionScore 0-100):
        {{dimensions}}

        Sentence logs (LLM-graded production records):
        {{logLines}}

        Scenario word stats (word mastery per life scenario):
        {{scenarioLines}}

        Reading behavior: sessionCount={{request.Reading.SessionCount}} avgLookupCount={{request.Reading.AvgLookupCount}}

        Return only JSON:
        {
          "findings": [
            {
              "dimension": "skill",
              "dimensionKey": "grammar",
              "polarity": "weakness",
              "statement": "一句中文结论，点名具体行为",
              "evidence": [
                { "kind": "sentence_log", "refId": "<log id>", "metric": "grammar", "op": "<=", "value": 2 }
              ],
              "confidence": "medium"
            }
          ]
        }

        Rules:
        - 3 to 8 findings; cover at least two dimensions when data allows. Data-poor dimensions may be omitted.
        - At most ONE finding per dimension+dimensionKey combination; merge weaker duplicates into the strongest one.
        - Do NOT reuse the same evidence across findings: each sentence_log id / word_stats scenario / reading_stats metric / assessment_dimension metric may be cited by only ONE finding.
        - dimension must be exactly ONE word: scenario, skill, or reading. Do NOT copy a list like "scenario|skill|reading".
        - polarity must be exactly ONE word: strength, weakness, or neutral. confidence must be exactly ONE word: high, medium, or low.
        - dimensionKey: scenario key for scenario findings; grammar|natural|vocabulary|relevance for skill; "reading" for reading.
        - evidence kind must be one of:
          sentence_log — refId = an id from the log list above; metric = grammar|natural|vocabulary|relevance (optional).
          assessment_dimension — refId = "final"; metric = grammar|natural|vocabulary|relevance|expressionScore.
          word_stats — refId = a scenario key above; metric = coverage|avgMastery|correctRate.
          reading_stats — refId = "reading"; metric = sessionCount|avgLookupCount.
        - op must be one of <=, >=, <, >, =. The claimed value MUST equal the actual value shown above (a verifier will re-check it mechanically).
        - confidence: high requires >=3 evidence entries, medium >=2, low >=1.
        - statement: one concise Chinese sentence naming the concrete behavior, e.g. "点餐场景核心动词掌握弱，check/order 类词造句错误率高".
        - NEVER invent ids, scenario keys, or numbers not shown above.
        """;
    }

    /// <summary>
    /// InsightAgent 提示词（T-007，DESIGN-bottleneck-insight §2.2）：给筛查信号 + 近期产出原文，
    /// 要求贴近原文判断瓶颈性质（不只是看分数），证据只引用给定 SentenceLog id（防幻觉靠机械过滤）。
    /// </summary>
    public static string BuildBottleneckInsightPrompt(BottleneckInsightRequest request)
    {
        var productionLines = request.Productions.Count == 0
            ? "(none)"
            : string.Join("\n", request.Productions.Select(sample =>
                $"- id={sample.Id} word={sample.TargetWord} scene={sample.Scene} grammar={sample.Grammar} natural={sample.Natural} vocabulary={sample.Vocabulary} relevance={sample.Relevance} errorTags=[{string.Join(",", sample.ErrorTags)}] text=\"{sample.Text}\""));

        var focus = request.PlanFocusScenarios.Count == 0 ? "(none)" : string.Join(", ", request.PlanFocusScenarios);
        var targets = request.PlanSentenceTargets.Count == 0 ? "(none)" : string.Join(", ", request.PlanSentenceTargets);

        return $$"""
        You are the Insight agent of an English learning app. Rule-based screening flagged signals about this learner. Read the ORIGINAL sentences below closely — judge the PRIMARY nature of the current bottleneck from the writing itself, not just from the scores.

        User level: {{request.UserLevel}}
        Screening signals: {{string.Join(", ", request.Signals)}}
        Current plan focus scenarios: {{focus}}
        Current plan sentence targets (words the plan asks the learner to produce): {{targets}}

        Recent production (LLM-graded, 0-5 per dimension, with original text):
        {{productionLines}}

        Return only JSON:
        {
          "nature": "grammar_errors",
          "statement": "一句中文结论，点名具体行为",
          "evidenceLogIds": ["<log id>"]
        }

        Rules:
        - nature must be exactly ONE word from this list (do NOT copy the whole list):
          vocabulary_insufficient — 词汇量不足：想表达但词不够，反复用最简单的词或卡壳。
          cannot_organize_sentences — 会词但组织不成句：用词尚可但句子破碎、语序混乱。
          grammar_errors — 语法错误多：时态/单复数/主谓一致等错误频繁。
          monotonous_expression — 语法正确但表达单调：句子都对但句式词汇重复、没有变化。
          avoidance_pattern — 回避模式：只写简单句，回避从句与复杂连接，能力范围收缩。
          chinglish_collocation — 中式搭配：搭配直译中文、不地道。
          safe_word_strategy — 安全词策略：新学的目标词从不进入自由产出，只用早已熟练的词。
        - evidenceLogIds: 1-5 ids from the production list above that best support the judgment. NEVER invent ids.
        - statement: one concise Chinese sentence naming the concrete behavior, e.g. "造句几乎全是主谓宾短句，because/which 从句近两周完全消失".
        - Pick the SINGLE most important nature even if several apply.
        """;
    }
}
