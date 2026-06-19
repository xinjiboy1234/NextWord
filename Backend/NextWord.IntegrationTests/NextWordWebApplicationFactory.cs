using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NextWord.Infrastructure.Data;

namespace NextWord.IntegrationTests;

public sealed class NextWordWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;
    private bool _initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IHostedService));

            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));

            _connection = new SqliteConnection($"Data Source=nextword-test-{Guid.NewGuid():N};Mode=Memory;Cache=Private");
            _connection.Open();

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        if (_initialized)
        {
            return host;
        }

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        SeedData.InitializeAsync(db).GetAwaiter().GetResult();
        _initialized = true;
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Dispose();
        }

        base.Dispose(disposing);
    }
}

[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<NextWordWebApplicationFactory>;
