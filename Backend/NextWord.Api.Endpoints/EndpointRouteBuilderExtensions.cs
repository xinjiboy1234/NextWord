namespace NextWord.Api.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNextWordEndpoints(this IEndpointRouteBuilder app)
    {
        WordEndpoints.Map(app);
        LearningEndpoints.Map(app);
        ProgressEndpoints.Map(app);
        LlmEndpoints.Map(app);
        return app;
    }
}
