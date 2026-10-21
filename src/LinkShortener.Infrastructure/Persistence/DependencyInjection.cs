using LinkShortener.Application.Links;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LinkShortener.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ShortLinks") ?? "Data Source=shortlinks.db";
        var connectionStringBuilder = new SqliteConnectionStringBuilder(connectionString)
        {
            DefaultTimeout = 5
        };

        services.AddDbContext<LinkDbContext>(options =>
            options.UseSqlite(connectionStringBuilder.ConnectionString));
        services.AddScoped<ILinkRepository, EfLinkRepository>();
        services.AddSingleton<SqliteTransientRetryPolicy>();
        return services;
    }
}
