using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NextWord.Infrastructure.Data;

namespace NextWord.IntegrationTests;

public sealed class NextWordWebApplicationFactory : WebApplicationFactory<Program>
{
    private bool _initialized;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll(typeof(IHostedService));
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
        PostgresTestDatabaseBootstrap.EnsureMigrated(db);
        SeedData.InitializeAsync(db).GetAwaiter().GetResult();
        _initialized = true;
        return host;
    }
}

[CollectionDefinition("Integration")]
public sealed class IntegrationCollection : ICollectionFixture<NextWordWebApplicationFactory>;
