using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace NextWord.IntegrationTests;

public static class IntegrationTestAuth
{
    public static async Task<HttpClient> CreateAuthenticatedClientAsync(NextWordWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "password123",
            displayName = "Integration Test",
        });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.Token);
        return client;
    }

    private sealed record AuthResponse(string Token);
}
