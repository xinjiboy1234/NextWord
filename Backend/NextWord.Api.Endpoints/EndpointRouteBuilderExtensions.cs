namespace NextWord.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNextWordEndpoints(this IEndpointRouteBuilder app)
    {
        WordEndpoints.Map(app);
        AuthEndpoints.Map(app);
        ProfileEndpoints.Map(app);
        LearningEndpoints.Map(app);
        ProgressEndpoints.Map(app);
        ProfileScoreEndpoints.Map(app);
        LlmEndpoints.Map(app);
        SentenceEndpoints.Map(app);
        FreeExpressionEndpoints.Map(app);
        SpellingEndpoints.Map(app);
        LogEndpoints.Map(app);
        ArticleEndpoints.Map(app);
        ReadingLogEndpoints.Map(app);
        CommentEndpoints.Map(app);
        ReadingAgentEndpoints.Map(app);
        AssessmentEndpoints.Map(app);
        ChallengeEndpoints.Map(app);
        LevelEndpoints.Map(app);
        ScoreKernelEndpoints.Map(app);
        return app;
    }
}
