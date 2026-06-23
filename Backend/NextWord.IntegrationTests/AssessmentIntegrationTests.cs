using System.Net;
using System.Net.Http.Json;

namespace NextWord.IntegrationTests;

[Collection("Integration")]
public class AssessmentIntegrationTests(NextWordWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Start_initial_assessment_requires_authentication()
    {
        var response = await _client.PostAsJsonAsync("/api/assessment/initial/start", new { });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Start_initial_assessment_returns_assessment_id_when_authenticated()
    {
        var client = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var response = await client.PostAsJsonAsync("/api/assessment/initial/start", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<StartAssessmentResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload!.AssessmentId);
    }

    [Fact]
    public async Task Get_progress_requires_authentication()
    {
        var response = await _client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_progress_includes_assessment_flag_when_authenticated()
    {
        var client = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var response = await client.GetAsync("/api/progress");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var progress = await response.Content.ReadFromJsonAsync<ProgressResponse>();
        Assert.NotNull(progress);
        Assert.False(progress!.HasCompletedInitialAssessment);
    }

    private sealed record StartAssessmentResponse(Guid AssessmentId, string Status);
    private sealed record ProgressResponse(bool HasCompletedInitialAssessment, bool IsUpgradeCandidate, int PendingReviewCount);
}
