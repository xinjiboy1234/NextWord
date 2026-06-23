namespace NextWord.Domain.Interfaces;

public interface IUserLlmProviderFactory
{
    Task<ILLMProvider> GetForUserAsync(Guid userId, CancellationToken cancellationToken);
}
