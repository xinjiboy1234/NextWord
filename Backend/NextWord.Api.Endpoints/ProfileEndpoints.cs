using Microsoft.EntityFrameworkCore;
using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;
using NextWord.Domain.Models;
using NextWord.Infrastructure.Data;
using NextWord.Infrastructure.Services;
using System.Text.Json;

namespace NextWord.Api.Endpoints;

public static class ProfileEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/profile").WithTags("Profile");

        group.MapGet("/llm/presets", () => Results.Ok(LlmPresets.All.Select(item => new LlmPresetDto(
            item.Id,
            item.Name,
            item.Provider.ToString(),
            item.BaseUrl,
            item.DefaultModel))));

        // T-005：最新 WeaknessProfile（含 Finding 核查状态；存疑条目标注但不进前端展示）
        group.MapGet("/weakness", async (
            HttpContext http,
            IWeaknessProfileService weaknessProfiles,
            CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var profile = await weaknessProfiles.GetLatestAsync(userId.Value, ct);
            if (profile is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new WeaknessProfileDto(
                profile.Id,
                profile.AssessmentId,
                profile.CreatedAt,
                profile.Findings.Select(finding => new ProfileFindingDto(
                    finding.Id,
                    finding.Dimension.ToString().ToLowerInvariant(),
                    finding.DimensionKey,
                    finding.Polarity.ToString().ToLowerInvariant(),
                    finding.Statement,
                    finding.Confidence.ToString().ToLowerInvariant(),
                    finding.Verification.ToString().ToLowerInvariant(),
                    finding.VerificationNote,
                    JsonSerializer.Deserialize<List<EvidenceClaim>>(finding.EvidenceJson, JsonOptions) ?? [])).ToList()));
        }).RequireAuthorization();

        group.MapGet("/", async (
            HttpContext http,
            IUserRepository users,
            LevelDashboardService dashboard,
            ApplicationDbContext db,
            CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var user = await users.GetByIdAsync(userId.Value, ct);
            if (user is null)
            {
                return Results.NotFound();
            }

            var progress = await users.GetOrCreateProgressAsync(user.Id, ct);
            var level = await dashboard.GetDashboardAsync(user.Id, ct);
            var llmSettings = await users.GetLlmSettingsAsync(user.Id, ct);

            var now = DateTimeOffset.UtcNow;
            var totalLearned = await db.UserWordRelationships.CountAsync(item => item.UserId == user.Id, ct);
            var reviewDueDates = await db.UserWordRelationships
                .Where(item => item.UserId == user.Id)
                .Select(item => item.NextReviewDue)
                .ToListAsync(ct);
            var dueReviews = reviewDueDates.Count(nextReviewDue => nextReviewDue <= now);
            var totalLogs = await db.WordLearningLogs.CountAsync(item => item.UserId == user.Id, ct);
            var correctLogs = await db.WordLearningLogs.CountAsync(item => item.UserId == user.Id && item.IsCorrect, ct);

            return Results.Ok(new ProfileDto(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                progress.OverallLevel.ToString(),
                progress.VocabLevel.ToString(),
                progress.SpellingLevel.ToString(),
                progress.SentenceLevel.ToString(),
                progress.ReadingLevel.ToString(),
                progress.StreakDays,
                progress.LastStudyDate,
                progress.HasCompletedInitialAssessment,
                progress.IsUpgradeCandidate,
                totalLearned,
                dueReviews,
                progress.PendingReviewCount,
                totalLogs,
                totalLogs == 0 ? 0 : Math.Round((double)correctLogs / totalLogs * 100, 1),
                level.RecentHistory.Select(item => new LevelHistoryItemDto(
                    item.Id,
                    item.FromLevel.ToString(),
                    item.ToLevel.ToString(),
                    item.Reason.ToString(),
                    item.Timestamp)).ToList(),
                llmSettings is null
                    ? null
                    : new UserLlmSettingsDto(
                        llmSettings.Provider.ToString(),
                        llmSettings.BaseUrl,
                        llmSettings.Model,
                        MaskApiKey(llmSettings.ApiKey),
                        !string.IsNullOrWhiteSpace(llmSettings.ApiKey))));
        }).RequireAuthorization();

        group.MapPut("/", async (
            HttpContext http,
            UpdateProfileRequest request,
            IUserRepository users,
            CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var user = await users.GetByIdAsync(userId.Value, ct);
            if (user is null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                user.DisplayName = request.DisplayName.Trim();
                await users.SaveChangesAsync(ct);
            }

            return Results.Ok(new AuthUserDto(user.Id, user.Email ?? string.Empty, user.DisplayName));
        }).RequireAuthorization();

        group.MapPut("/llm", async (
            HttpContext http,
            UpdateLlmSettingsRequest request,
            IUserRepository users,
            CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var preset = LlmPresets.FindById(request.PresetId);
            var provider = preset?.Provider ?? ParseProvider(request.Provider);
            var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
                ? preset?.BaseUrl ?? "https://api.openai.com/v1"
                : request.BaseUrl.Trim();
            var model = string.IsNullOrWhiteSpace(request.Model)
                ? preset?.DefaultModel ?? "gpt-4o-mini"
                : request.Model.Trim();

            var existing = await users.GetLlmSettingsAsync(userId.Value, ct);
            var apiKey = string.IsNullOrWhiteSpace(request.ApiKey)
                ? existing?.ApiKey
                : request.ApiKey.Trim();

            var settings = new UserLlmSettings
            {
                UserId = userId.Value,
                Provider = provider,
                BaseUrl = baseUrl,
                Model = model,
                ApiKey = apiKey,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var saved = await users.UpsertLlmSettingsAsync(settings, ct);
            return Results.Ok(new UserLlmSettingsDto(
                saved.Provider.ToString(),
                saved.BaseUrl,
                saved.Model,
                MaskApiKey(saved.ApiKey),
                !string.IsNullOrWhiteSpace(saved.ApiKey)));
        }).RequireAuthorization();
    }

    private static LlmProviderType ParseProvider(string? value)
        => Enum.TryParse<LlmProviderType>(value, true, out var parsed) ? parsed : LlmProviderType.OpenAI;

    private static string? MaskApiKey(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        if (apiKey.Length <= 8)
        {
            return "****";
        }

        return $"{apiKey[..4]}...{apiKey[^4..]}";
    }
}

public sealed record UpdateProfileRequest(string? DisplayName);

public sealed record WeaknessProfileDto(
    long Id,
    Guid? AssessmentId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ProfileFindingDto> Findings);

public sealed record ProfileFindingDto(
    long Id,
    string Dimension,
    string DimensionKey,
    string Polarity,
    string Statement,
    string Confidence,
    string Verification,
    string VerificationNote,
    IReadOnlyList<EvidenceClaim> Evidence);
public sealed record UpdateLlmSettingsRequest(
    string? PresetId,
    string? Provider,
    string? BaseUrl,
    string? Model,
    string? ApiKey);

public sealed record LlmPresetDto(string Id, string Name, string Provider, string BaseUrl, string DefaultModel);

public sealed record UserLlmSettingsDto(
    string Provider,
    string BaseUrl,
    string Model,
    string? MaskedApiKey,
    bool HasApiKey);

public sealed record LevelHistoryItemDto(
    Guid Id,
    string FromLevel,
    string ToLevel,
    string Reason,
    DateTimeOffset ChangedAt);

public sealed record ProfileDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string OverallLevel,
    string VocabLevel,
    string SpellingLevel,
    string SentenceLevel,
    string ReadingLevel,
    int StreakDays,
    DateOnly? LastStudyDate,
    bool HasCompletedInitialAssessment,
    bool IsUpgradeCandidate,
    int TotalLearned,
    int DueReviews,
    int PendingReviewCount,
    int TotalLogs,
    double AccuracyPercent,
    IReadOnlyList<LevelHistoryItemDto> RecentHistory,
    UserLlmSettingsDto? LlmSettings);
