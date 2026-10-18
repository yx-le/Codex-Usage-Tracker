using CodexUsageTracker.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class HistoryAndResetTests
{
    [Fact] public void OfflineGapsStayBlankAndSmallResetClockDriftDoesNotFragmentLines()
    {
        var now = DateTimeOffset.UtcNow;
        var reset = now.AddHours(2);
        UsageSnapshot Sample(int minutes, int resetSeconds) => new(now.AddMinutes(minutes), "Codex app-server", new(20, 300, reset.AddSeconds(resetSeconds)), null);
        var segments = HistorySeries.Build(new[] { Sample(-120, 0), Sample(-110, 30), Sample(-10, 30), Sample(-5, 60) }, s => s.FiveHour, now);
        Assert.Equal(2, segments.Count);
        Assert.All(segments, s => Assert.Equal(2, s.Count));
    }
    [Fact] public void DayViewExcludesEarlierDaysAndInvalidPercentages()
    {
        var now = DateTimeOffset.UtcNow;
        UsageSnapshot Sample(int hours, double used) => new(now.AddHours(hours), "Codex app-server", new(used, 300, null), null);
        var samples = new[] { Sample(-48, 10), Sample(-2, -1), Sample(-1, 25), Sample(0, 101) };
        var day = HistorySeries.Build(samples, s => s.FiveHour, now, 1);
        Assert.Equal(75, Assert.Single(Assert.Single(day)).Remaining);
        Assert.Equal(2, HistorySeries.Build(samples, s => s.FiveHour, now, 7).Count);
    }
    [Fact] public void LinesAreChronologicalDeduplicatedAndSplitAtReset()
    {
        var now = DateTimeOffset.UtcNow;
        var reset = now.AddHours(1);
        UsageSnapshot Sample(int minutes, double used, DateTimeOffset end, string source = "Codex app-server") => new(now.AddMinutes(minutes), source, new(used, 300, end), null);
        var segments = HistorySeries.Build(new[] { Sample(-20, 20, reset), Sample(-30, 10, reset), Sample(-20, 99, reset, "Local Codex fallback"), Sample(-10, 0, reset.AddHours(5)) }, s => s.FiveHour, now);
        Assert.Equal(2, segments.Count);
        Assert.Equal(new[] { 90d, 80d }, segments[0].Select(p => p.Remaining));
        Assert.Equal(100, Assert.Single(segments[1]).Remaining);
    }
    [Fact] public void MissingExpiredAndOutOfRangeSamplesAreNotDrawnAsQuota()
    {
        var now = DateTimeOffset.UtcNow;
        var samples = new[] {
            new UsageSnapshot(now.AddDays(-8), "Live", new(30, 300, null), null),
            new UsageSnapshot(now.AddDays(1), "Live", new(30, 300, null), null),
            new UsageSnapshot(now, "Live", new(100, 300, now.AddMinutes(-1)), null),
            UsageSnapshot.Unavailable(now.AddMinutes(-1)) };
        Assert.Empty(HistorySeries.Build(samples, s => s.FiveHour, now));
    }
    [Fact] public void ResetDateUsesRequestedTimezoneIncludingDateRollover()
    {
        var reset = new DateTimeOffset(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test +08", TimeSpan.FromHours(8), "Test +08", "Test +08");
        var actual = new QuotaWindow(20, 300, reset).ResetDateTime(zone);
        Assert.Contains("19", actual); Assert.Contains("2026", actual); Assert.Contains("07:30", actual); Assert.Contains("UTC+08:00", actual);
        Assert.Equal("Reset date unavailable", new QuotaWindow(20, 300, null).ResetDateTime(zone));
    }
    [Fact] public async Task HistorySurvivesFreshRepositoryAndRestoresFromWalAwareBackup()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "usage.db"); var now = DateTimeOffset.UtcNow;
            var first = new AsyncUsageRepository(path);
            await first.SaveAndReadAsync(new(now.AddHours(-1), "Codex app-server", new(20, 300, now.AddHours(2)), null));
            await first.SaveAndReadAsync(new(now, "Codex app-server", new(30, 300, now.AddHours(2)), null));
            Assert.Equal(2, (await new AsyncUsageRepository(path).ReadAsync()).Count);
            using (var backup = new SqliteConnection($"Data Source={path}.bak"))
            { backup.Open(); using var command = backup.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM samples"; Assert.Equal(2L, command.ExecuteScalar()); }
            SqliteConnection.ClearAllPools();
            File.Delete(path); File.Delete(path + "-wal"); File.Delete(path + "-shm");
            var recovered = new AsyncUsageRepository(path);
            Assert.Equal(2, (await recovered.ReadAsync()).Count);
            await recovered.ClearAsync(); SqliteConnection.ClearAllPools(); File.Delete(path);
            Assert.Empty(await new AsyncUsageRepository(path).ReadAsync()); // Clear must clear recovery data too.
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
}
