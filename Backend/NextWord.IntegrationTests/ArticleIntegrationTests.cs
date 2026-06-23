using System.Net;
using System.Net.Http.Json;

namespace NextWord.IntegrationTests;

[Collection("Integration")]
public class ArticleIntegrationTests(NextWordWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_articles_requires_authentication()
    {
        var response = await _client.GetAsync("/api/articles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_articles_returns_seeded_list_when_authenticated()
    {
        var client = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var response = await client.GetAsync("/api/articles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var articles = await response.Content.ReadFromJsonAsync<List<ArticleSummaryDto>>();
        Assert.NotNull(articles);
        Assert.NotEmpty(articles!);
    }

    private sealed record ArticleSummaryDto(Guid Id, string Title);
}
