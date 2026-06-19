using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using NextWord.Api.Endpoints;
using NextWord.Api.HealthChecks;
using NextWord.Infrastructure;
using NextWord.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("database")
    .AddCheck<LlmHealthCheck>("llm");
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
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

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", name = "NextWord.Api" }))
    .WithTags("Health");
app.MapHealthChecks("/api/health/details");
app.MapNextWordEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(db);
}

app.Run();
