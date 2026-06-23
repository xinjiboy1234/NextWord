using Microsoft.AspNetCore.Authorization;
using NextWord.Domain.Interfaces;

namespace NextWord.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // 注册/登录为全站唯一匿名业务入口
        group.MapPost("/register", async (RegisterRequest request, IAuthService auth, CancellationToken ct) =>
        {
            try
            {
                var result = await auth.RegisterAsync(request.Email, request.Password, request.DisplayName ?? request.Email, ct);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }).AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, IAuthService auth, CancellationToken ct) =>
        {
            var result = await auth.LoginAsync(request.Email, request.Password, ct);
            return result is null
                ? Results.Unauthorized()
                : Results.Ok(result);
        }).AllowAnonymous();

        group.MapGet("/me", async (HttpContext http, IUserRepository users, CancellationToken ct) =>
        {
            var userId = UserResolver.GetAuthenticatedUserId(http);
            if (!userId.HasValue)
            {
                return Results.Unauthorized();
            }

            var user = await users.GetByIdAsync(userId.Value, ct);
            return user is null
                ? Results.NotFound()
                : Results.Ok(new AuthUserDto(user.Id, user.Email ?? string.Empty, user.DisplayName));
        }).RequireAuthorization();
    }
}

public sealed record RegisterRequest(string Email, string Password, string? DisplayName);
public sealed record LoginRequest(string Email, string Password);
