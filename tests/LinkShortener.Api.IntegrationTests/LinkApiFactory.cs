using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LinkShortener.Infrastructure.Persistence;

namespace LinkShortener.Api.IntegrationTests;

public sealed class LinkApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseDirectory;
    private readonly bool _deleteDatabaseDirectoryOnDispose;

    public LinkApiFactory()
        : this(Path.Combine(Path.GetTempPath(), $"link-shortener-tests-{Guid.NewGuid():N}", "links.db"), true)
    {
    }

    private LinkApiFactory(string databasePath, bool deleteDatabaseDirectoryOnDispose)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        _databaseDirectory = Path.GetDirectoryName(DatabasePath)!;
        _deleteDatabaseDirectoryOnDispose = deleteDatabaseDirectoryOnDispose;
    }

    public string DatabasePath { get; }

    public static LinkApiFactory ForDatabase(string databasePath) => new(databasePath, false);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_databaseDirectory);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ShortLinks"] = $"Data Source={DatabasePath}",
                ["ShortLinks:PublicOrigin"] = "http://localhost"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<LinkDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LinkDbContext>>();
            services.AddDbContext<LinkDbContext>(options =>
                options.UseSqlite($"Data Source={DatabasePath};Default Timeout=5;Pooling=False"));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _deleteDatabaseDirectoryOnDispose && Directory.Exists(_databaseDirectory))
        {
            Directory.Delete(_databaseDirectory, recursive: true);
        }
    }
}
