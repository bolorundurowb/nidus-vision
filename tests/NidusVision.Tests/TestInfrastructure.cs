using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NidusVision.Data;

namespace NidusVision.Tests;

public abstract class SqliteTestBase : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    protected AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AppDbContext(options);
    }
}

public sealed class TestDirectory : IDisposable
{
    public TestDirectory(string category = "nidus-tests")
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            category,
            Guid.CreateVersion7().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] parts) =>
        parts.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
