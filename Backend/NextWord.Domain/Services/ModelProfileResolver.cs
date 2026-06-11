using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;

namespace NextWord.Domain.Services;

public sealed class ModelProfileResolver : IModelProfileResolver
{
    private readonly Dictionary<string, ModelProfile> _profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["local-dev"] = new ModelProfile(),
        ["grading-stable"] = new ModelProfile
        {
            Id = "grading-stable",
            Provider = "Mock",
            Model = "mock-grading-stable-v1",
            Temperature = 0,
            MaxOutputTokens = 600,
            TimeoutSeconds = 10
        }
    };

    public ModelProfile Resolve(string? modelProfileId)
    {
        if (!string.IsNullOrWhiteSpace(modelProfileId) && _profiles.TryGetValue(modelProfileId, out var profile))
        {
            return profile;
        }

        return _profiles["local-dev"];
    }
}
