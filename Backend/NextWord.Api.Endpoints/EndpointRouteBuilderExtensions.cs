namespace NextWord.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNextWordEndpoints(this IEndpointRouteBuilder app)
    {
        WordEndpoints.Map(app);
        LearningEndpoints.Map(app);
        ProgressEndpoints.Map(app);
        LlmEndpoints.Map(app);
        SentenceEndpoints.Map(app);
        FreeExpressionEndpoints.Map(app);
        SpellingEndpoints.Map(app);
        LogEndpoints.Map(app);
        return app;
    }
}
