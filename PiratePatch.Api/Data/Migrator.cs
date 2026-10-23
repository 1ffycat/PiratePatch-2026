using System.Reflection;
using Npgsql;

namespace PiratePatch.Api.Data;

/// <summary>Applies the embedded <c>Data/Migrations/*.sql</c> files once each, in name order. Safe to run from several instances.</summary>
public sealed class Migrator(NpgsqlDataSource db, ILogger<Migrator> logger) : IHostedService
{
    // Arbitrary constant; serialises concurrent migrators.
    private const long AdvisoryLockKey = 0x50_49_52_41_54_45;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await db.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await Execute(connection, transaction, "SELECT pg_advisory_xact_lock($1)", cancellationToken, AdvisoryLockKey);
        await Execute(connection, transaction,
            "CREATE TABLE IF NOT EXISTS schema_migrations (version text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())",
            cancellationToken);

        var assembly = Assembly.GetExecutingAssembly();
        var migrations = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

        foreach (var resource in migrations)
        {
            var version = Path.GetFileNameWithoutExtension(resource.Split(".Migrations.")[^1]);

            await using var check = new NpgsqlCommand("SELECT 1 FROM schema_migrations WHERE version = $1", connection, transaction)
            {
                Parameters = { new() { Value = version } },
            };
            if (await check.ExecuteScalarAsync(cancellationToken) is not null)
                continue;

            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
            await Execute(connection, transaction, await reader.ReadToEndAsync(cancellationToken), cancellationToken);
            await Execute(connection, transaction, "INSERT INTO schema_migrations (version) VALUES ($1)", cancellationToken, version);

            logger.LogInformation("Applied database migration {Version}", version);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params object[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in parameters)
            command.Parameters.Add(new NpgsqlParameter { Value = value });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
