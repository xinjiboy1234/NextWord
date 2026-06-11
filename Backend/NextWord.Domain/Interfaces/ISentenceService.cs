using NextWord.Domain.Entities;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface ISentenceService
{
    Task<IReadOnlyList<Sentence>> GetPromptsAsync(int count, CancellationToken cancellationToken);
    Task<SentenceLog> RateAsync(Guid userId, Guid? wordId, string targetWord, string userSentence, string scene, string userLevel, CancellationToken cancellationToken);
}
