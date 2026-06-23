using NextWord.Domain.Interfaces;
using NextWord.Domain.Services;

namespace NextWord.Infrastructure.Services;

public sealed class UserLlmProviderFactory(
    IUserRepository users,
    ILLMProvider globalProvider,
    LlmMockProvider fallback) : IUserLlmProviderFactory
{
    public async Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await users.GetLlmSettingsAsync(userId, cancellationToken);
        if (settings is null || string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return globalProvider;
        }

        try
        {
            var chatClient = LlmClientFactory.CreateChatClient(settings.Model, settings.ApiKey, settings.BaseUrl);
            return new LlmChatClientProvider(chatClient, fallback);
        }
        catch
        {
            return globalProvider;
        }
    }
}
