using System.Security.Cryptography;
using Npgsql;

namespace PiratePatch.Api.Players;

public sealed record Solve(string CertificateId, long TelegramId, string Username, DateTimeOffset SolvedAt, long Rank);

public sealed record LeaderboardEntry(long Rank, string Username, DateTimeOffset SolvedAt, string CertificateId);

public sealed record Leaderboard(long Total, IReadOnlyList<LeaderboardEntry> Entries);

public sealed class PlayerStore(NpgsqlDataSource db)
{
    // The rank of a solve is its position when ordered by time, ties broken by id.
    private const string SolveColumns = """
        s.certificate_id, s.telegram_id, s.username, s.solved_at,
        (SELECT count(*) FROM solves o WHERE (o.solved_at, o.telegram_id) <= (s.solved_at, s.telegram_id)) AS rank
        """;

    /// <returns>True when this is the player's first ever login.</returns>
    public async Task<bool> RecordLoginAsync(long telegramId, string username, CancellationToken ct)
    {
        // xmax is zero on a freshly inserted row and set on one that was updated.
        await using var command = db.CreateCommand("""
            INSERT INTO players (telegram_id, username) VALUES ($1, $2)
            ON CONFLICT (telegram_id) DO UPDATE SET username = EXCLUDED.username, last_login_at = now()
            RETURNING (xmax = 0)
            """);
        command.Parameters.AddWithValue(telegramId);
        command.Parameters.AddWithValue(username);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }

    public async Task<Solve?> FindSolveAsync(long telegramId, CancellationToken ct)
    {
        await using var command = db.CreateCommand($"SELECT {SolveColumns} FROM solves s WHERE s.telegram_id = $1");
        command.Parameters.AddWithValue(telegramId);
        return await ReadSolveAsync(command, ct);
    }

    public async Task<Solve?> FindSolveByCertificateAsync(string certificateId, CancellationToken ct)
    {
        await using var command = db.CreateCommand($"SELECT {SolveColumns} FROM solves s WHERE s.certificate_id = $1");
        command.Parameters.AddWithValue(certificateId);
        return await ReadSolveAsync(command, ct);
    }

    /// <summary>Records the first solve; later calls return the original one. <c>Created</c> tells which happened.</summary>
    public async Task<(Solve Solve, bool Created)> RecordSolveAsync(long telegramId, string username, CancellationToken ct)
    {
        await using var insert = db.CreateCommand("""
            INSERT INTO solves (telegram_id, certificate_id, username) VALUES ($1, $2, $3)
            ON CONFLICT (telegram_id) DO NOTHING
            """);
        insert.Parameters.AddWithValue(telegramId);
        insert.Parameters.AddWithValue(NewCertificateId());
        insert.Parameters.AddWithValue(username);
        var created = await insert.ExecuteNonQueryAsync(ct) == 1;

        var solve = await FindSolveAsync(telegramId, ct)
            ?? throw new InvalidOperationException("The solve vanished right after being recorded.");
        return (solve, created);
    }

    public async Task<Leaderboard> GetLeaderboardAsync(int limit, int offset, CancellationToken ct)
    {
        await using var command = db.CreateCommand("""
            SELECT count(*) OVER () AS total,
                   row_number() OVER (ORDER BY solved_at, telegram_id) AS rank,
                   username, solved_at, certificate_id
            FROM solves
            ORDER BY solved_at, telegram_id
            LIMIT $1 OFFSET $2
            """);
        command.Parameters.AddWithValue(limit);
        command.Parameters.AddWithValue(offset);

        var entries = new List<LeaderboardEntry>();
        long total = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            total = reader.GetInt64(0);
            entries.Add(new(reader.GetInt64(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetString(4)));
        }

        if (entries.Count == 0 && offset > 0)
        {
            await using var count = db.CreateCommand("SELECT count(*) FROM solves");
            total = (long)(await count.ExecuteScalarAsync(ct))!;
        }

        return new(total, entries);
    }

    public async Task<bool> PingAsync(CancellationToken ct)
    {
        await using var command = db.CreateCommand("SELECT 1");
        return await command.ExecuteScalarAsync(ct) is 1;
    }

    private static async Task<Solve?> ReadSolveAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new Solve(reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), reader.GetInt64(4))
            : null;
    }

    private static string NewCertificateId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(10));
}
