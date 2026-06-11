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
        return Task.FromResult(new SentenceRatingResponse(80, 78, 75, ["Mock provider keeps sentence scoring as a placeholder."]));
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
