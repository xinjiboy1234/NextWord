namespace NextWord.Domain.Interfaces;

public sealed record WebSearchResult(string Title, string Snippet, string Url);

public interface IWebSearchService
{
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
