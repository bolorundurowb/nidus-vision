using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NidusVision.Core.Models;
using NidusVision.Core.Options;

namespace NidusVision.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddNidusData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var dataDir = Path.GetFullPath(storage.DataDirectory);
            Directory.CreateDirectory(dataDir);
            var dbPath = Path.Combine(dataDir, storage.DatabaseFileName);
            options.UseSqlite($"Data Source={dbPath}");
        });
        return services;
    }

    public static async Task InitializeNidusDatabaseAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await db.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=5000;", cancellationToken);

        if (!await db.AppSettings.AnyAsync(cancellationToken))
        {
            db.AppSettings.Add(new AppSettings());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
