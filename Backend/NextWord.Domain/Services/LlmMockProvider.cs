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
        var meanings = new[]
        {
            new Meaning($"{request.Word} 的常见中文含义", true, request.Context ?? string.Empty)
        };

        return Task.FromResult(new DefinitionResponse(
            request.Word,
            string.Empty,
            meanings,
            [$"{request.Word} phrase"],
            [$"I learned the word {request.Word} today."],
            "Mock definition for MVP.",
            rating.Difficulty,
            rating.Cefr));
    }

    public Task<SentenceRatingResponse> RateSentenceAsync(SentenceRatingRequest request, CancellationToken cancellationToken)
    {
        var sentence = request.UserSentence.Trim();
        var target = request.TargetWord.Trim();
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
            analysis.Add($"Try to use the target word \"{target}\" directly.");
        }

        if (!hasSentenceShape)
        {
            analysis.Add("Write a complete sentence with clear punctuation.");
        }

        if (analysis.Count == 0)
        {
            analysis.Add("Clear expression with usable sentence structure.");
        }

        return Task.FromResult(new SentenceRatingResponse(
            grammar,
            natural,
            vocabulary,
            relevance,
            grade,
            revision,
            analysis,
            average >= 4 ? DifficultyLevel.Intermediate : DifficultyLevel.Basic,
            usesTarget ? "Add one detail to make the sentence more specific." : "Make the target meaning explicit in your sentence."));
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
