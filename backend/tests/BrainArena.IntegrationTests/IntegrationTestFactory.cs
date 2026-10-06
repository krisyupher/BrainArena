using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BrainArena.IntegrationTests;

/// <summary>
/// Boots the real Api host against a dedicated, uniquely-named database on the same Postgres
/// server the docker-compose file provides for local dev (each instance gets its own database,
/// since xUnit can run different test classes' fixtures in parallel and a shared fixed name would
/// race on drop/create). Needs `docker compose up -d` to be running first — this talks to that
/// Postgres directly rather than spinning up its own container (e.g. via Testcontainers), which
/// also avoids pulling in a Docker-automation NuGet package that some locked-down Windows
/// machines' Application Control policies block from loading at all.
/// </summary>
public class IntegrationTestFactory : WebApplicationFactory<Program>, Xunit.IAsyncLifetime
{
    // Must match docker-compose.yml / backend/src/BrainArena.Api/appsettings.json.
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Username=brainarena;Password=brainarena_dev_password;Database=postgres";

    private readonly string _testDatabaseName = $"brainarena_test_{Guid.NewGuid():N}";

    private string TestConnectionString =>
        $"Host=localhost;Port=5432;Username=brainarena;Password=brainarena_dev_password;Database={_testDatabaseName}";

    async Task Xunit.IAsyncLifetime.InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using var createCmd = connection.CreateCommand();
        createCmd.CommandText = $"CREATE DATABASE {_testDatabaseName};";
        await createCmd.ExecuteNonQueryAsync();
    }

    async Task Xunit.IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using var dropCmd = connection.CreateCommand();
        dropCmd.CommandText = $"DROP DATABASE IF EXISTS {_testDatabaseName} WITH (FORCE);";
        await dropCmd.ExecuteNonQueryAsync();
    }

    /// <summary>Per-fixture config overrides, applied on top of the defaults below (later source wins).</summary>
    protected virtual IReadOnlyDictionary<string, string?> ConfigOverrides => new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestConnectionString,
                // No admin bootstrap needed for these tests.
                ["Admin:Email"] = "",
                ["Admin:Password"] = "",
                // Keep the fixed countdown/reveal overhead low — the per-question answer window
                // itself (10-60s) is a real product rule (RoomValidation) and stays untouched.
                ["MatchTiming:CountdownSeconds"] = "1",
                ["MatchTiming:RevealSeconds"] = "1",
                // Every TestServer request shares one "unknown" client IP, so real limits would
                // throttle ordinary suites; RateLimitingTests opts back into small ones.
                ["RateLimiting:Auth:TokenLimit"] = "100000",
                ["RateLimiting:Auth:TokensPerMinute"] = "100000",
                ["RateLimiting:GuestCreation:TokenLimit"] = "100000",
                ["RateLimiting:GuestCreation:TokensPerMinute"] = "100000",
                ["RateLimiting:RoomCreation:TokenLimit"] = "100000",
                ["RateLimiting:RoomCreation:TokensPerMinute"] = "100000",
                // GuestAccountTests drives IGuestCleanup directly instead of waiting on the timer.
                ["GuestCleanup:Enabled"] = "false"
            });
            config.AddInMemoryCollection(ConfigOverrides);
        });
    }
}
