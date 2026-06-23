namespace NextWord.Infrastructure.Auth;

public sealed class AuthOptions
{
    public string JwtSecret { get; set; } = "nextword-dev-jwt-secret-change-in-production-32chars";
    public string Issuer { get; set; } = "NextWord";
    public string Audience { get; set; } = "NextWord";
    public int ExpirationDays { get; set; } = 7;
}
