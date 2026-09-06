using System.Diagnostics;
using BattleTanks.Domain.Entities;
using BattleTanks.Infrastructure.Persistence.Context;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// LB-7 Activity 1 — measures ToList vs AsNoTracking and bulk insert throughput.
// Usage: dotnet run --project tools/BattleTanks.DbBenchmark -- --scores 5000

var scoreCount = ParseScoreCount(args);
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("PostgreSQL")
    ?? BuildConnectionStringFromEnv();

var services = new ServiceCollection();
services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

Console.WriteLine("BattleTanks DB Benchmark (LB-7 Activity 1)");
Console.WriteLine($"Connection: {MaskConnection(connectionString)}");
Console.WriteLine($"Score seed size: {scoreCount}");
Console.WriteLine();

await db.Database.MigrateAsync();

var player = await EnsureBenchmarkPlayerAsync(db);
await ClearBenchmarkScoresAsync(db, player.Id);

var seedWatch = Stopwatch.StartNew();
var scores = Enumerable.Range(0, scoreCount)
    .Select(i => new Score
    {
        Id = Guid.NewGuid(),
        PlayerId = player.Id,
        Points = Random.Shared.Next(1, 10_000),
        Date = DateTime.UtcNow.AddMinutes(-i)
    })
    .ToList();
await db.BulkInsertAsync(scores);
seedWatch.Stop();
Console.WriteLine($"BulkInsert {scoreCount} scores: {seedWatch.ElapsedMilliseconds} ms");

const int iterations = 8;
var trackedMs = await MeasureAsync(iterations, async () =>
{
    await using var measureScope = provider.CreateAsyncScope();
    var measureDb = measureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    _ = await measureDb.Scores
        .OrderByDescending(score => score.Points)
        .Take(50)
        .ToListAsync();
});

var noTrackingMs = await MeasureAsync(iterations, async () =>
{
    await using var measureScope = provider.CreateAsyncScope();
    var measureDb = measureScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    _ = await measureDb.Scores
        .AsNoTracking()
        .OrderByDescending(score => score.Points)
        .Take(50)
        .ToListAsync();
});

Console.WriteLine($"Top-50 ToList (tracked) avg: {trackedMs:F2} ms");
Console.WriteLine($"Top-50 AsNoTracking avg:     {noTrackingMs:F2} ms");
Console.WriteLine($"Speedup AsNoTracking:        {(trackedMs / Math.Max(noTrackingMs, 0.01)):F2}x");
Console.WriteLine();
Console.WriteLine("Tip: capture EXPLAIN ANALYZE before/after indexes for Lab07-activity1.md screenshots.");
return;

static int ParseScoreCount(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--scores" && int.TryParse(args[i + 1], out var parsed))
        {
            return Math.Clamp(parsed, 100, 100_000);
        }
    }

    return 5_000;
}

static async Task<double> MeasureAsync(int iterations, Func<Task> action)
{
    // Warm-up
    await action();
    var watch = Stopwatch.StartNew();
    for (var i = 0; i < iterations; i++)
    {
        await action();
    }

    watch.Stop();
    return watch.Elapsed.TotalMilliseconds / iterations;
}

static async Task<Player> EnsureBenchmarkPlayerAsync(ApplicationDbContext db)
{
    const string email = "lb7-benchmark@battletanks.local";
    var existing = await db.Players.FirstOrDefaultAsync(player => player.Email == email);
    if (existing is not null)
    {
        return existing;
    }

    var player = new Player
    {
        Name = "LB7 Benchmark",
        Email = email,
        PasswordHash = "benchmark-not-a-real-hash"
    };
    db.Players.Add(player);
    await db.SaveChangesAsync();
    return player;
}

static async Task ClearBenchmarkScoresAsync(ApplicationDbContext db, Guid playerId)
{
    await db.Scores.Where(score => score.PlayerId == playerId).ExecuteDeleteAsync();
}

static string BuildConnectionStringFromEnv()
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var db = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "battle_tanks";
    var user = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "battle_tanks_user";
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "battle_tanks_password";
    return $"Host={host};Port={port};Database={db};Username={user};Password={password}";
}

static string MaskConnection(string connectionString)
{
    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return string.Join(';', parts.Select(part =>
        part.StartsWith("Password=", StringComparison.OrdinalIgnoreCase) ? "Password=***" : part));
}
