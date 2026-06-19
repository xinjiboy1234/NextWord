using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class ArticleEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/articles").WithTags("Articles");

        group.MapGet("/", async (DifficultyLevel? level, CefrLevel? cefr, IArticleService articles, CancellationToken ct) =>
        {
            var list = await articles.ListAsync(level, cefr, ct);
            return Results.Ok(list.Select(ArticleDto.FromEntity));
        });

        group.MapGet("/{id:guid}", async (Guid id, IArticleService articles, CancellationToken ct) =>
        {
            var article = await articles.GetByIdAsync(id, ct);
            return article is null ? Results.NotFound() : Results.Ok(ArticleDetailDto.FromEntity(article));
        });

        group.MapPost("/{id:guid}/reading/start", async (
            Guid id,
            StartReadingRequest request,
            IUserRepository users,
            IArticleService articles,
            CancellationToken ct) =>
        {
            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            try
            {
                var log = await articles.StartReadingAsync(user.Id, id, ct);
                return Results.Ok(ReadingLogDto.FromEntity(log));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });

        group.MapPost("/{id:guid}/vocab-extract", async (
            Guid id,
            VocabExtractRequestBody request,
            IUserRepository users,
            IArticleVocabService vocab,
            CancellationToken ct) =>
        {
            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            try
            {
                var mappings = await vocab.ExtractAndPersistAsync(id, user.Id, ct);
                return Results.Ok(mappings.Select(ArticleVocabMappingDto.FromEntity));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });

        group.MapGet("/{id:guid}/vocab", async (Guid id, IArticleVocabService vocab, CancellationToken ct) =>
        {
            var mappings = await vocab.GetMappingsAsync(id, ct);
            return Results.Ok(mappings.Select(ArticleVocabMappingDto.FromEntity));
        });

        group.MapPost("/{id:guid}/lookup", async (
            Guid id,
            WordLookupRequest request,
            IArticleVocabService vocab,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Word))
            {
                return Results.BadRequest(new { message = "Word is required." });
            }

            var definition = await vocab.LookupWordAsync(id, request.Word, request.Context, ct);
            return definition is null ? Results.NotFound() : Results.Ok(definition);
        });
    }
}

public static class ReadingLogEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reading-logs").WithTags("ReadingLogs");

        group.MapPost("/{logId:guid}/finish", async (
            Guid logId,
            FinishReadingRequest request,
            IArticleService articles,
            CancellationToken ct) =>
        {
            var log = await articles.FinishReadingAsync(logId, request.LookupCount, request.CommentsCount, ct);
            return log is null ? Results.NotFound() : Results.Ok(ReadingLogDto.FromEntity(log));
        });

        group.MapPost("/{logId:guid}/lookup", async (Guid logId, IArticleService articles, CancellationToken ct) =>
        {
            await articles.IncrementLookupAsync(logId, ct);
            return Results.Ok(new { message = "Lookup recorded." });
        });
    }
}

public static class CommentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/articles/{articleId:guid}/comments").WithTags("Comments");

        group.MapGet("/", async (Guid articleId, ICommentService comments, CancellationToken ct) =>
        {
            var list = await comments.ListAsync(articleId, ct);
            return Results.Ok(list.Select(ArticleCommentDto.FromEntity));
        });

        group.MapPost("/", async (
            Guid articleId,
            AddCommentRequest request,
            IUserRepository users,
            ICommentService comments,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.CommentText))
            {
                return Results.BadRequest(new { message = "Comment text is required." });
            }

            var user = request.UserId.HasValue
                ? await users.GetByIdAsync(request.UserId.Value, ct)
                : await users.GetOrCreateDefaultUserAsync(ct);
            if (user is null)
            {
                return Results.NotFound(new { message = "User not found." });
            }

            try
            {
                var comment = await comments.AddAsync(
                    user.Id,
                    articleId,
                    request.ParagraphIndex,
                    request.ParagraphText ?? string.Empty,
                    request.CommentText,
                    request.RequestAiReply,
                    ct);
                return Results.Ok(ArticleCommentDto.FromEntity(comment));
            }
            catch (InvalidOperationException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });
    }
}

public static class ReadingAgentEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/reading/agent", async (
            ReadingAgentRequestBody request,
            IReadingAgentService agent,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Intent) || string.IsNullOrWhiteSpace(request.ArticleContent))
            {
                return Results.BadRequest(new { message = "Intent and article content are required." });
            }

            var response = await agent.AssistAsync(new Domain.Models.ReadingAgentRequest(
                request.Intent,
                request.ArticleTitle ?? "Untitled",
                request.ArticleContent,
                request.SelectedWord,
                request.ParagraphText,
                request.UserLevel ?? "A2"), ct);
            return Results.Ok(response);
        }).WithTags("ReadingAgent");
    }
}

public sealed record StartReadingRequest(Guid? UserId);
public sealed record VocabExtractRequestBody(Guid? UserId);
public sealed record WordLookupRequest(string Word, string? Context);
public sealed record FinishReadingRequest(int LookupCount, int CommentsCount);
public sealed record AddCommentRequest(Guid? UserId, int ParagraphIndex, string? ParagraphText, string CommentText, bool RequestAiReply = false);
public sealed record ReadingAgentRequestBody(string Intent, string? ArticleTitle, string ArticleContent, string? SelectedWord, string? ParagraphText, string? UserLevel);

public sealed record ArticleDto(
    Guid Id,
    string Title,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    int WordCount,
    ArticleSource Source,
    string? TopicTag)
{
    public static ArticleDto FromEntity(Article article) => new(
        article.Id,
        article.Title,
        article.DifficultyLevel,
        article.CefrLevel,
        article.WordCount,
        article.Source,
        article.TopicTag);
}

public sealed record ArticleDetailDto(
    Guid Id,
    string Title,
    string Content,
    DifficultyLevel DifficultyLevel,
    CefrLevel CefrLevel,
    int WordCount,
    ArticleSource Source,
    string? TopicTag,
    IReadOnlyList<ArticleVocabMappingDto> VocabMappings)
{
    public static ArticleDetailDto FromEntity(Article article) => new(
        article.Id,
        article.Title,
        article.Content,
        article.DifficultyLevel,
        article.CefrLevel,
        article.WordCount,
        article.Source,
        article.TopicTag,
        article.VocabMappings.Select(ArticleVocabMappingDto.FromEntity).ToList());
}

public sealed record ArticleVocabMappingDto(
    Guid Id,
    string WordLemma,
    string ContextMeaning,
    string SpecialUsage,
    DifficultyLevel DifficultyInContext,
    RecommendedAction RecommendedAction,
    bool IsKeyVocab)
{
    public static ArticleVocabMappingDto FromEntity(ArticleVocabMapping mapping) => new(
        mapping.Id,
        mapping.WordLemma,
        mapping.ContextMeaning,
        mapping.SpecialUsage,
        mapping.DifficultyInContext,
        mapping.RecommendedAction,
        mapping.IsKeyVocab);
}

public sealed record ReadingLogDto(
    Guid Id,
    Guid ArticleId,
    DateTimeOffset StartTime,
    DateTimeOffset? EndTime,
    int DurationSeconds,
    int LookupCount,
    int CommentsCount)
{
    public static ReadingLogDto FromEntity(ReadingLog log) => new(
        log.Id,
        log.ArticleId,
        log.StartTime,
        log.EndTime,
        log.DurationSeconds,
        log.LookupCount,
        log.CommentsCount);
}

public sealed record ArticleCommentDto(
    Guid Id,
    int ParagraphIndex,
    string ParagraphText,
    string CommentText,
    string? AiReply,
    DateTimeOffset Timestamp)
{
    public static ArticleCommentDto FromEntity(ArticleComment comment) => new(
        comment.Id,
        comment.ParagraphIndex,
        comment.ParagraphText,
        comment.CommentText,
        comment.AiReply,
        comment.Timestamp);
}
