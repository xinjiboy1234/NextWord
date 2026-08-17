using Microsoft.EntityFrameworkCore;
using Npgsql;
using NextWord.Infrastructure.Data;

namespace NextWord.UnitTests;

/// <summary>
/// 单元测试用 PostgreSQL：连接 <c>nextword_unit_test</c>。
/// 需本地 Postgres（如 <c>docker compose up -d postgres</c>）。
/// </summary>
internal static class PostgresTestDatabase
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _initialized;

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("ConnectionStrings__UnitTestPostgreSql")
        ?? "Host=localhost;Port=5432;Database=nextword_unit_test;Username=nextword;Password=nextword";

    public static async Task<ApplicationDbContext> CreateContextAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// T-061：创建一次性隔离数据库（唯一库名 + EnsureCreated），用于依赖「库内无其他词」的确定性断言
    /// （共享库 nextword_unit_test 在 xUnit 类间并行下会被其他测试类并发造词，无法构造「全部词已学」场景）。
    /// 用完须调用 <see cref="ApplicationDbContext.Database"/>.EnsureDeletedAsync() 清理。
    /// </summary>
    public static async Task<ApplicationDbContext> CreateIsolatedContextAsync(CancellationToken cancellationToken = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Database = $"nextword_unit_iso_{Guid.NewGuid():N}"
        };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(builder.ConnectionString)
            .Options;
        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        return db;
    }

    private static async Task EnsureInitializedAsync(CancellationToken cancellationToken)
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

            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            InitLock.Release();
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
