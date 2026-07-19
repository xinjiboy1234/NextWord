using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IArticleVocabService
{
    Task<IReadOnlyList<ArticleVocabMapping>> GetMappingsAsync(Guid articleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ArticleVocabMapping>> ExtractAndPersistAsync(Guid articleId, Guid userId, CancellationToken cancellationToken);
    Task<ArticleWordDetailResult> GetOrCreateWordDetailAsync(
        Guid articleId,
        Guid userId,
        string word,
        string? context,
        CancellationToken cancellationToken);
    Task<ArticleWordDetailResult> LookupWordAsync(
        Guid articleId,
        Guid userId,
        string word,
        string? context,
        CancellationToken cancellationToken);
}
