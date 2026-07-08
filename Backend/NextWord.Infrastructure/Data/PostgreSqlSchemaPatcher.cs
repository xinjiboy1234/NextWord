using Microsoft.EntityFrameworkCore;

namespace NextWord.Infrastructure.Data;

/// <summary>
/// 在 PostgreSQL 上补齐 Score 内核 schema。AddScoreKernelM1 含 SQLite 专用 ALTER，无法对已有 PG 库直接 Migrate。
/// </summary>
public static class PostgreSqlSchemaPatcher
{
    private static readonly string PatchSql = LoadPatchSql();

    public static async Task ApplyAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (!db.Database.IsNpgsql())
        {
            return;
        }

        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(PatchSql, cancellationToken);
    }

    private static string LoadPatchSql()
    {
        var assembly = typeof(PostgreSqlSchemaPatcher).Assembly;
        const string resourceName = "NextWord.Infrastructure.Data.Patch_PostgreSql_ScoreKernel.sql";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
