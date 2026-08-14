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

    [Fact]
    public async Task List_assessments_requires_authentication()
    {
        var response = await _client.GetAsync("/api/assessments");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>T-054：列表只含本人测评、按开始时间倒序（新→旧）。</summary>
    [Fact]
    public async Task List_assessments_returns_only_own_assessments_descending()
    {
        var clientA = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var firstResponse = await clientA.PostAsJsonAsync("/api/assessment/initial/start", new { });
        var first = await firstResponse.Content.ReadFromJsonAsync<StartAssessmentResponse>();
        await clientA.PostAsJsonAsync("/api/assessment/initial/skip", new { });
        var secondResponse = await clientA.PostAsJsonAsync("/api/assessment/initial/start", new { });
        var second = await secondResponse.Content.ReadFromJsonAsync<StartAssessmentResponse>();

        var clientB = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var otherResponse = await clientB.PostAsJsonAsync("/api/assessment/initial/start", new { });
        var other = await otherResponse.Content.ReadFromJsonAsync<StartAssessmentResponse>();

        var response = await clientA.GetAsync("/api/assessments");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<AssessmentListItemResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Equal(second!.AssessmentId, list[0].Id);
        Assert.Equal(first!.AssessmentId, list[1].Id);
        Assert.DoesNotContain(list, item => item.Id == other!.AssessmentId);
        Assert.True(list[0].StartAt >= list[1].StartAt);
        Assert.Equal("Completed", list[1].Status);
        Assert.Equal("A2", list[1].FinalLevel);
    }

    /// <summary>T-054：详情归属校验——本人 200 且响应体可完整解析（records 非空），他人 id 一律 404。</summary>
    [Fact]
    public async Task Get_assessment_detail_of_other_user_returns_404()
    {
        var clientA = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var createdResponse = await clientA.PostAsJsonAsync("/api/assessment/initial/start", new { });
        var created = await createdResponse.Content.ReadFromJsonAsync<StartAssessmentResponse>();

        // 取第一块出题后详情即有记录（records 非空），覆盖循环引用序列化失败的盲区（qa-t054 D1）
        var blockResponse = await clientA.GetAsync($"/api/assessment/{created!.AssessmentId}/next-block");
        Assert.Equal(HttpStatusCode.OK, blockResponse.StatusCode);

        var own = await clientA.GetAsync($"/api/assessment/{created.AssessmentId}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        var detail = await own.Content.ReadFromJsonAsync<AssessmentDetailResponse>();
        Assert.NotNull(detail);
        Assert.Equal(created.AssessmentId, detail!.Id);
        Assert.NotEmpty(detail.Records);
        Assert.Contains(detail.Records, record => record.Step == "AdaptiveBlock" && record.QuestionsJson != "[]");

        var clientB = await IntegrationTestAuth.CreateAuthenticatedClientAsync(factory);
        var foreign = await clientB.GetAsync($"/api/assessment/{created.AssessmentId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    private sealed record AssessmentListItemResponse(
        Guid Id,
        string Type,
        string Status,
        DateTimeOffset StartAt,
        DateTimeOffset? EndAt,
        string? FinalLevel,
        int? ExpressionScore,
        bool GuardAdjusted);

    private sealed record AssessmentDetailResponse(
        Guid Id,
        string Type,
        string Status,
        DateTimeOffset StartAt,
        DateTimeOffset? EndAt,
        string? FinalLevel,
        List<AssessmentRecordResponse> Records);

    private sealed record AssessmentRecordResponse(
        Guid Id,
        string Step,
        string QuestionType,
        string QuestionsJson,
        string AnswersJson,
        string ScoresJson,
        DateTimeOffset Timestamp);

    private sealed record StartAssessmentResponse(Guid AssessmentId, string Status);
    private sealed record ProgressResponse(bool HasCompletedInitialAssessment, bool IsUpgradeCandidate, int PendingReviewCount);
}
