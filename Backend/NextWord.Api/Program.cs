using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NextWord.Api.Endpoints;
using NextWord.Api.HealthChecks;
using NextWord.Infrastructure;
using NextWord.Infrastructure.Auth;
using NextWord.Infrastructure.Data;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.Configure<HostOptions>(options =>
{
    // Worker 异常不应拖垮 API 进程
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("database")
    .AddCheck<LlmHealthCheck>("llm");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authOptions.Issuer,
            ValidAudience = authOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.JwtSecret))
        };
    });
// 全站默认要求登录；仅显式 AllowAnonymous 的端点可匿名访问
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddNextWordInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", name = "NextWord.Api" }))
    .WithTags("Health")
    .AllowAnonymous();
app.MapHealthChecks("/api/health/details").AllowAnonymous();
app.MapNextWordEndpoints();

if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Development / AutoMigrate：先 EF 迁移（T-015 起迁移链在 PG 上可完整跑通，
        // 失败即抛错快速失败，不再吞错带病进种子），再 PG 幂等补丁（Score 内核）
        var autoMigrate = app.Configuration.GetValue<bool>("Database:AutoMigrate");
        if (app.Environment.IsDevelopment() || autoMigrate)
        {
            await db.Database.MigrateAsync();

            await PostgreSqlSchemaPatcher.ApplyAsync(db);
        }

        await SeedData.InitializeAsync(db);
    }
}

app.Run();

public partial class Program;
