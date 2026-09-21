using System.Text.Json;

namespace CodexUsageTracker.Core;

public sealed class LocalCodexSource
{
    public UsageSnapshot? Read(string codexHome, CancellationToken token)
    {
        var sessionRoot = Path.Combine(codexHome, "sessions");
        if (!Directory.Exists(sessionRoot)) return null;
        UsageSnapshot? newest = null;
        // Bounded fallback: seven date directories, 24 recent files, final 1 MiB each.
        var files = Enumerable.Range(0, 8).Select(day => DateTime.UtcNow.AddDays(-day))
            .Select(date => Path.Combine(sessionRoot, date.ToString("yyyy"), date.ToString("MM"), date.ToString("dd")))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.TopDirectoryOnly))
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).Take(24);
        foreach (var file in files)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var skipPartial = stream.Length > 1024 * 1024;
                if (skipPartial) stream.Seek(-1024 * 1024, SeekOrigin.End);
                using var reader = new StreamReader(stream);
                if (skipPartial) reader.ReadLine();
                while (reader.ReadLine() is { } line)
                {
                    token.ThrowIfCancellationRequested();
                    if (!line.Contains("\"rate_limits\"", StringComparison.Ordinal)) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("type", out var kind) || kind.GetString() != "event_msg"
                            || !root.TryGetProperty("payload", out var payload)
                            || !payload.TryGetProperty("type", out var type) || type.GetString() != "token_count"
                            || !root.TryGetProperty("timestamp", out var stamp) || !stamp.TryGetDateTimeOffset(out var observed)) continue;
                        var snapshot = QuotaParser.Parse(payload, observed, "Local Codex fallback");
                        if (snapshot.HasData && observed <= DateTimeOffset.UtcNow.AddMinutes(1)
                            && (newest is null || observed > newest.ObservedAt)) newest = snapshot;
                    }
                    catch (JsonException) { /* A partially written final record is expected. */ }
                    catch (InvalidOperationException) { /* Unsupported record shape. */ }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return newest;
    }
}
