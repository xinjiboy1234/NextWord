namespace NextWord.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken);
    Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed record AuthResult(string Token, AuthUserDto User);

public sealed record AuthUserDto(Guid Id, string Email, string DisplayName);
