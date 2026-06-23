using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NextWord.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NextWord.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<AuthOptions> options) : IJwtTokenService
{
    public string CreateToken(User user)
    {
        var authOptions = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: authOptions.Issuer,
            audience: authOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(authOptions.ExpirationDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public interface IJwtTokenService
{
    string CreateToken(User user);
}
