using NextWord.Domain.Interfaces;

namespace NextWord.Infrastructure.Auth;

public sealed class AuthService(IUserRepository users, IJwtTokenService jwt) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Email and password are required.");
        }

        if (password.Length < 6)
        {
            throw new InvalidOperationException("Password must be at least 6 characters.");
        }

        var existing = await users.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException("Email is already registered.");
        }

        var hash = PasswordHasher.Hash(password);
        var user = await users.CreateUserAsync(email, hash, displayName, cancellationToken);
        var token = jwt.CreateToken(user);
        return new AuthResult(token, new AuthUserDto(user.Id, user.Email!, user.DisplayName));
    }

    public async Task<AuthResult?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.GetByEmailAsync(email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return null;
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        var token = jwt.CreateToken(user);
        return new AuthResult(token, new AuthUserDto(user.Id, user.Email!, user.DisplayName));
    }
}
