using NextWord.Domain.Models;

namespace NextWord.Domain.Interfaces;

public interface IReadingAgentService
{
    Task<ReadingAgentResponse> AssistAsync(ReadingAgentRequest request, CancellationToken cancellationToken);
}
