using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public sealed class LlmMockProvider(IModelProfileResolver modelProfileResolver) : ILLMProvider
{
    private static readonly Dictionary<string, (DifficultyLevel Difficulty, CefrLevel Cefr, RecommendedAction Action)> Ratings =
        BuildRatings();
    private static readonly (DifficultyLevel Difficulty, CefrLevel Cefr, RecommendedAction Action) DefaultRating =
        (DifficultyLevel.Basic, CefrLevel.A1, RecommendedAction.LearnNow);

    public Task<DifficultyRating> RateDifficultyAsync(ItemRatingRequest request, CancellationToken cancellationToken)
    {
        var profile = modelProfileResolver.Resolve(request.Options?.ModelProfileId);
        var key = request.Text.Trim().ToLowerInvariant();
        var rating = Ratings.GetValueOrDefault(key, DefaultRating);

        return Task.FromResult(new DifficultyRating(
            request.ItemType,
            rating.Difficulty,
            rating.Cefr,
            $"Mock rating from {profile.Id}.",
            rating.Action,
            0.86,
            profile.Id));
    }

    public Task<DefinitionResponse> GetDefinitionAsync(DefinitionRequest request, CancellationToken cancellationToken)
    {
        var key = request.Word.Trim().ToLowerInvariant();
        var rating = Ratings.GetValueOrDefault(key, DefaultRating);
        var useChinese = ExplanationLanguageHelper.IsChinese(
            ExplanationLanguageHelper.Resolve(request.ExplanationLanguage, ExplanationLanguageHelper.Default));
        var definition = useChinese
            ? BuildChineseMockDefinition(key, request.Context)
            : BuildEnglishMockDefinition(key, request.Context);

        return Task.FromResult(new DefinitionResponse(
            request.Word,
            MockPhonetics.TryGetValue(key, out var phonetic) ? phonetic : $"/{key}/",
            [new Meaning(definition, true, request.Context ?? string.Empty)],
            useChinese ? [$"与 {key} 相关的搭配"] : [$"{key} collocation"],
            BuildMockExamples(request.Word, request.Context, useChinese),
            useChinese ? "注意该词在上下文中的具体用法。" : "Note how this word is used in context.",
            rating.Difficulty,
            rating.Cefr));
    }

    public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
    {
        var sentence = request.UserSentence.Trim();
        var target = request.TargetWord.Trim();
        var useChinese = ExplanationLanguageHelper.IsChinese(
            ExplanationLanguageHelper.Resolve(request.ExplanationLanguage, ExplanationLanguageHelper.Default));
        var isFreeExpression = string.Equals(target, "free expression", StringComparison.OrdinalIgnoreCase);
        var usesTarget = isFreeExpression || sentence.Contains(target, StringComparison.OrdinalIgnoreCase);
        var hasSentenceShape = sentence.Length >= 12 && (sentence.EndsWith('.') || sentence.EndsWith('!') || sentence.EndsWith('?'));
        var grammar = hasSentenceShape ? 4 : 3;
        var natural = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 5 ? 4 : 3;
        var vocabulary = usesTarget ? 4 : 2;
        var relevance = usesTarget ? 4 : 2;
        var average = (grammar + natural + vocabulary + relevance) / 4.0;
        var grade = average >= 4.5 ? "A" : average >= 3.8 ? "B" : average >= 2.8 ? "C" : "D";
        var revision = hasSentenceShape ? sentence : ToCompleteSentence(sentence);
        var analysis = new List<string>();
        if (!usesTarget)
        {
            analysis.Add(useChinese
                ? $"请直接在句子中使用目标词 \"{target}\"。"
                : $"Try to use the target word \"{target}\" directly.");
        }

        if (!hasSentenceShape)
        {
            analysis.Add(useChinese
                ? "请写一句结构完整、标点清晰的英文句子。"
                : "Write a complete sentence with clear punctuation.");
        }

        if (analysis.Count == 0)
        {
            analysis.Add(useChinese
                ? "表达清晰，句子结构可用。"
                : "Clear expression with usable sentence structure.");
        }

        var suggestion = usesTarget
            ? useChinese ? "补充一个细节，让句子更具体。" : "Add one detail to make the sentence more specific."
            : useChinese ? "在句子中明确体现目标词的含义。" : "Make the target meaning explicit in your sentence.";

        return Task.FromResult(new SentenceRatingResponse(
            grammar,
            natural,
            vocabulary,
            relevance,
            grade,
            revision,
            analysis,
            average >= 4 ? DifficultyLevel.Intermediate : DifficultyLevel.Basic,
            suggestion));
    }

    public Task<VocabExtractResponse> ExtractVocabAsync(VocabExtractRequest request, CancellationToken cancellationToken)
    {
        var useChinese = ExplanationLanguageHelper.IsChinese(
            ExplanationLanguageHelper.Resolve(request.ExplanationLanguage, ExplanationLanguageHelper.Default));
        var words = request.ArticleContent
            .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '"', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim().Trim('"', '\'', '—', '-').ToLowerInvariant())
            .Where(word => word.Length >= 4 && char.IsLetter(word[0]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        var keyVocab = words
            .Where(word => !Ratings.TryGetValue(word, out var rating) || rating.Difficulty != DifficultyLevel.Basic)
            .Take(8)
            .Select(word =>
            {
                var rating = Ratings.GetValueOrDefault(word, (DifficultyLevel.Intermediate, CefrLevel.B1, RecommendedAction.LearnNow));
                return new KeyVocabItem(
                    word,
                    MockPhonetics.TryGetValue(word, out var phonetic) ? phonetic : $"/{word}/",
                    useChinese
                        ? $"在本文中指「{word}」的上下文含义"
                        : $"Contextual meaning of \"{word}\" in this article.",
                    new WordExample(
                        WordExampleKind.Contextual,
                        $"The word \"{word}\" appears in this article.",
                        useChinese
                            ? "用法精髓：注意该词在本文语境中的具体含义。"
                            : "Essence: notice how the word is used in this article."),
                    new WordExample(
                        WordExampleKind.General,
                        $"I often hear \"{word}\" in daily conversation.",
                        useChinese
                            ? "其他场景：日常对话中的常见用法。"
                            : "Other scenario: common everyday usage."),
                    rating.Item1,
                    rating.Item3);
            })
            .ToList();

        var skippedBasic = words.Where(word => Ratings.TryGetValue(word, out var rating) && rating.Difficulty == DifficultyLevel.Basic).Take(5).ToList();
        var skippedRare = words.Where(word => word.Length >= 10).Take(3).ToList();

        return Task.FromResult(new VocabExtractResponse(keyVocab, skippedBasic, skippedRare));
    }

    public Task<CommentReplyResponse> ReplyToCommentAsync(CommentReplyRequest request, CancellationToken cancellationToken)
    {
        var reply = $"This paragraph discusses \"{request.ParagraphText[..Math.Min(40, request.ParagraphText.Length)]}...\". " +
                    $"Regarding your comment: {request.CommentText.Trim()} — consider how the author uses context to clarify meaning.";
        return Task.FromResult(new CommentReplyResponse(reply));
    }

    private static string BuildChineseMockDefinition(string word, string? context)
    {
        if (MockChineseDefinitions.TryGetValue(word, out var definition))
        {
            return definition;
        }

        return string.IsNullOrWhiteSpace(context)
            ? $"常见名词/动词，基本含义与「{word}」相关"
            : $"在句中「{TruncateContext(context)}」里，指与「{word}」相关的含义";
    }

    private static string BuildEnglishMockDefinition(string word, string? context)
    {
        return string.IsNullOrWhiteSpace(context)
            ? $"A common English word: {word}"
            : $"In context \"{TruncateContext(context)}\", {word} refers to its contextual meaning.";
    }

    private static string TruncateContext(string context)
    {
        var trimmed = context.Trim();
        return trimmed.Length <= 60 ? trimmed : $"{trimmed[..57]}...";
    }

    private static readonly Dictionary<string, string> MockChineseDefinitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cart"] = "手推车；流动售货车（文中常指卖冰淇淋的小推车）",
            ["park"] = "公园；供休闲活动的户外场所",
            ["family"] = "家庭；一起生活的亲属群体",
            ["ice"] = "冰；冷冻的水",
            ["cream"] = "奶油；乳制品",
        };

    private static readonly Dictionary<string, string> MockPhonetics =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cart"] = "/kɑːrt/",
            ["park"] = "/pɑːrk/",
            ["family"] = "/ˈfæməli/",
            ["ice"] = "/aɪs/",
            ["cream"] = "/kriːm/",
        };

    private static IReadOnlyList<WordExample> BuildMockExamples(string word, string? context, bool useChinese)
    {
        var contextualSentence = string.IsNullOrWhiteSpace(context)
            ? $"We talked about {word} in class today."
            : context.Trim();
        return
        [
            new WordExample(
                WordExampleKind.Contextual,
                contextualSentence,
                useChinese ? "贴合当前语境：注意该词在句中的具体含义。" : "Contextual usage in the current sentence."),
            new WordExample(
                WordExampleKind.General,
                $"She used the word \"{word}\" in a different situation.",
                useChinese ? "其他场景：展示该词在日常中的延伸用法。" : "General usage in another everyday scenario.")
        ];
    }

    private static string ToCompleteSentence(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence))
        {
            return "I can write a complete sentence.";
        }

        var trimmed = sentence.Trim().TrimEnd('.', '!', '?');
        return $"{char.ToUpperInvariant(trimmed[0])}{trimmed[1..]}.";
    }

    private static Dictionary<string, (DifficultyLevel, CefrLevel, RecommendedAction)> BuildRatings()
    {
        var basic = new[]
        {
            "apple","book","cat","dog","eat","go","home","water","school","friend","happy","big","small","red","blue",
            "green","run","walk","read","write","listen","speak","day","night","morning","food","music","family","name",
            "city","car","bus","train","phone","computer","desk","chair","window","door","sun","moon","rain","snow",
            "hot","cold","good","bad","new","old","first","last"
        };
        var intermediate = new[]
        {
            "achieve","balance","culture","decision","effort","feature","growth","habit","improve","journey","knowledge",
            "language","memory","notice","opinion","practice","quality","reason","support","translate","useful","various",
            "wonder","accurate","benefit","compare","describe","environment","frequent","grammar","include","manage",
            "natural","observe","prepare","regular","similar","traditional","valuable"
        };
        var advanced = new[]
        {
            "ambiguous","comprehensive","derive","elaborate","framework","hypothesis","implicit","justify","nuance",
            "optimize","paradigm","qualitative","rigorous","synthesize","threshold","underlying","validate","approximate",
            "constraint","deduplicate"
        };

        var result = new Dictionary<string, (DifficultyLevel, CefrLevel, RecommendedAction)>(StringComparer.OrdinalIgnoreCase);
        foreach (var word in basic)
        {
            result[word] = (DifficultyLevel.Basic, CefrLevel.A1, RecommendedAction.LearnNow);
        }

        foreach (var word in intermediate)
        {
            result[word] = (DifficultyLevel.Intermediate, CefrLevel.B1, RecommendedAction.ReviewLater);
        }

        foreach (var word in advanced)
        {
            result[word] = (DifficultyLevel.Advanced, CefrLevel.C1, RecommendedAction.ChallengeOnly);
        }

        return result;
    }
}
