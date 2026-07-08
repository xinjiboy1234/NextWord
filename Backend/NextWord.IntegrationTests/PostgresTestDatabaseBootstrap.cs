using Microsoft.EntityFrameworkCore;
using Npgsql;
using NextWord.Infrastructure.Data;

namespace NextWord.IntegrationTests;

/// <summary>
/// 集成测试库 bootstrap：确保 <c>nextword_test</c> 存在且 EF 迁移已应用。
/// </summary>
internal static class PostgresTestDatabaseBootstrap
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql")
        ?? "Host=localhost;Port=5432;Database=nextword_test;Username=nextword;Password=nextword";

    public static void EnsureMigrated(ApplicationDbContext db)
    {
        EnsureMigratedAsync(db).GetAwaiter().GetResult();
    }

    public static async Task EnsureMigratedAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await InitLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await RecreateDatabaseAsync(cancellationToken);

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;
            await using (var fresh = new ApplicationDbContext(options))
            {
                await fresh.Database.EnsureCreatedAsync(cancellationToken);
            }

            if (!await CanQueryUsersAsync(cancellationToken))
            {
                throw new InvalidOperationException(
                    $"Integration test PostgreSQL schema is incomplete. Connection: {ConnectionString}");
            }

            _initialized = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static async Task<bool> CanQueryUsersAsync(CancellationToken cancellationToken)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new ApplicationDbContext(options);
        return await HasCoreSchemaAsync(db, cancellationToken);
    }

    private static async Task<bool> HasCoreSchemaAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1 FROM \"Users\" LIMIT 1", cancellationToken);
            return true;
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.UndefinedTable)
        {
            return false;
        }
    }

    private static async Task RecreateDatabaseAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.Database!;
        builder.Database = "postgres";

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var exists = connection.CreateCommand())
        {
            exists.CommandText = "SELECT 1 FROM pg_database WHERE datname = @name";
            exists.Parameters.AddWithValue("name", databaseName);
            var databaseExists = await exists.ExecuteScalarAsync(cancellationToken) is not null;
            if (!databaseExists)
            {
                await using var create = connection.CreateCommand();
                create.CommandText = $"""CREATE DATABASE "{databaseName.Replace("\"", "\"\"")}" """;
                await create.ExecuteNonQueryAsync(cancellationToken);
                return;
            }
        }

        await using (var terminate = connection.CreateCommand())
        {
            terminate.CommandText = """
                SELECT pg_terminate_backend(pid)
                FROM pg_stat_activity
                WHERE datname = @name AND pid <> pg_backend_pid()
                """;
            terminate.Parameters.AddWithValue("name", databaseName);
            await terminate.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"""DROP DATABASE IF EXISTS "{databaseName.Replace("\"", "\"\"")}" """;
            await drop.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var create = connection.CreateCommand())
        {
            create.CommandText = $"""CREATE DATABASE "{databaseName.Replace("\"", "\"\"")}" """;
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
    }
}
