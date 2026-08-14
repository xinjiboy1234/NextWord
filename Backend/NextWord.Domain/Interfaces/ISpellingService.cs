using NextWord.Domain.Entities;
using NextWord.Domain.Enums;
using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface ISpellingService
{
    /// <summary>T-052：拼写队列组装（复习/新词/混合三模式，mixed 新旧 3:7 互补位，新词按难度带内口径取）。</summary>
    Task<IReadOnlyList<SpellingQueueItem>> GetQueueAsync(Guid userId, int count, SpellingQueueMode mode, CancellationToken cancellationToken);

    Task<SpellingLog> SubmitAsync(Guid userId, Guid wordId, string userSpelling, int attempts, CancellationToken cancellationToken);
}
