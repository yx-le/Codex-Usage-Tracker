using Microsoft.Data.Sqlite;

namespace CodexUsageTracker.Core;

public sealed class UsageRepository
{
    private readonly string connectionString;
    public UsageRepository(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS samples (
                observed INTEGER NOT NULL, source TEXT NOT NULL,
                five_used REAL, five_reset INTEGER, week_used REAL, week_reset INTEGER,
                PRIMARY KEY(observed, source));
            """;
        command.ExecuteNonQuery();
        Prune(DateTimeOffset.UtcNow);
    }

    public void Save(UsageSnapshot sample)
    {
        if (!sample.HasData || sample.ObservedAt < DateTimeOffset.UtcNow.AddDays(-7)) return;
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO samples VALUES ($time,$source,$five,$fiveReset,$week,$weekReset)";
        command.Parameters.AddWithValue("$time", sample.ObservedAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$source", sample.Source);
        command.Parameters.AddWithValue("$five", (object?)sample.FiveHour?.UsedPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$fiveReset", (object?)sample.FiveHour?.ResetsAt?.ToUnixTimeSeconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("$week", (object?)sample.Weekly?.UsedPercent ?? DBNull.Value);
        command.Parameters.AddWithValue("$weekReset", (object?)sample.Weekly?.ResetsAt?.ToUnixTimeSeconds() ?? DBNull.Value);
        command.ExecuteNonQuery();
        Prune(DateTimeOffset.UtcNow);
    }

    public void Prune(DateTimeOffset now)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM samples WHERE observed < $cutoff";
        command.Parameters.AddWithValue("$cutoff", now.AddDays(-7).ToUnixTimeSeconds());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<UsageSnapshot> ReadHistory(DateTimeOffset now)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM samples WHERE observed >= $cutoff ORDER BY observed";
        command.Parameters.AddWithValue("$cutoff", now.AddDays(-7).ToUnixTimeSeconds());
        using var reader = command.ExecuteReader();
        var samples = new List<UsageSnapshot>();
        while (reader.Read())
        {
            QuotaWindow? Window(int used, int reset, int duration) => reader.IsDBNull(used) ? null
                : new(reader.GetDouble(used), duration, reader.IsDBNull(reset) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(reset)));
            samples.Add(new(DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)), reader.GetString(1), Window(2, 3, 300), Window(4, 5, 10080)));
        }
        return samples;
    }

    public void Clear()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA secure_delete=ON; DELETE FROM samples; PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
        command.ExecuteNonQuery();
    }
    private SqliteConnection Open() { var connection = new SqliteConnection(connectionString); connection.Open(); return connection; }
}
