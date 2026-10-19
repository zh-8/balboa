using System.Collections.Concurrent;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using Xunit;

namespace Balboa.TestKit;

/// <summary>
/// One real Postgres 17 container per test assembly. Register it in the test assembly with
/// <c>[assembly: AssemblyFixture(typeof(PostgresFixture))]</c> and take it as a constructor argument.
/// Call <see cref="ResetAsync"/> between tests (after migrations ran) to wipe data but keep the schema.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // BALBOA_TEST_POSTGRES_IMAGE exists only so an offline or rate-limited machine can substitute a locally cached
    // Postgres 17 image (e.g. postgres:17-alpine). CI uses the default.
    private static readonly string _image =
        Environment.GetEnvironmentVariable("BALBOA_TEST_POSTGRES_IMAGE") is { Length: > 0 } image ? image : "postgres:17";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(_image).Build();
    private readonly ConcurrentDictionary<string, Respawner> _respawners = new();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Deletes all rows in the given schemas, except the EF migrations history.</summary>
    public async Task ResetAsync(params string[] schemas)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        string key = string.Join('|', schemas);
        if (!_respawners.TryGetValue(key, out Respawner? respawner))
        {
            respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = schemas,
                TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")],
            });
            _respawners[key] = respawner;
        }

        await respawner.ResetAsync(connection);
    }
}
