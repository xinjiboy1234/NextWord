using NextWord.Domain.Entities;
using NextWord.Domain.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace NextWord.Api.Endpoints;

public static class UserResolver
{
    /// <summary>
    /// 全站要求登录：仅从 JWT 解析当前用户，不再回退 query userId 或默认种子用户。
    /// </summary>
    public static async Task<User?> ResolveAsync(
        HttpContext http,
        Guid? userId,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        _ = userId;

        var claimId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(claimId, out var fromJwt))
        {
            return null;
        }

        return await users.GetByIdAsync(fromJwt, cancellationToken);
    }

    public static Guid? GetAuthenticatedUserId(HttpContext http)
    {
        var claimId = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(claimId, out var id) ? id : null;
    }
}
