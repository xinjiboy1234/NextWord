using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

namespace NextWord.Infrastructure.Services;

public static class LlmClientFactory
{
    public static IChatClient CreateChatClient(string model, string apiKey, string baseUrl)
    {
        var endpoint = NormalizeEndpoint(baseUrl);
        var client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
        {
            Endpoint = endpoint
        });
        return client.GetChatClient(model).AsIChatClient();
    }

    private static Uri NormalizeEndpoint(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        if (!trimmed.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = $"https://{trimmed}";
        }

        return new Uri(trimmed);
    }
}
