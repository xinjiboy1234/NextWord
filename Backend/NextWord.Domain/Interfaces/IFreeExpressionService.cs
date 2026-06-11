using NextWord.Domain.Entities;

namespace NextWord.Domain.Interfaces;

public interface IFreeExpressionService
{
    Task<FreeExpressionLog> RateAsync(Guid userId, string userText, string userLevel, CancellationToken cancellationToken);
}
