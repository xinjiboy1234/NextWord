using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IArticleVocabService
{
    Task<IReadOnlyList<ArticleVocabMapping>> GetMappingsAsync(Guid articleId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ArticleVocabMapping>> ExtractAndPersistAsync(Guid articleId, Guid userId, CancellationToken cancellationToken);
    Task<DefinitionResponse?> LookupWordAsync(Guid articleId, string word, string? context, CancellationToken cancellationToken);
}
