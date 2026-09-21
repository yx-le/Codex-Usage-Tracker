using CodexUsageTracker.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class ReliabilityTests
{
    [Fact] public void ResetRetriesSurviveUnavailableAndPartialReadings()
    {
        var now = DateTimeOffset.UtcNow;
        var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), null), now));
        Assert.False(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddSeconds(14)));
        Assert.True(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddSeconds(15)));
        Assert.True(schedule.ShouldRefresh(new(now, "Live", null, new(20, 10080, now.AddDays(4))), now.AddSeconds(75)));
        Assert.False(schedule.ShouldRefresh(new(now, "Live", new(0, 300, now.AddHours(5)), null), now.AddSeconds(135)));
        // An older fallback cannot re-arm the previous reset.
        Assert.False(schedule.ShouldRefresh(new(now.AddMinutes(-1), "Local", new(100, 300, now), null), now.AddSeconds(195)));
    }

    [Fact] public void EachQuotaWindowRetainsItsOwnPendingReset()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), new(100, 10080, now)), now));
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(0, 300, now.AddHours(5)), null), now.AddSeconds(15)));
        Assert.False(schedule.ShouldRefresh(new(now, "Live", null, new(0, 10080, now.AddDays(7))), now.AddSeconds(75)));
    }

    [Fact] public void PendingRetriesExpireAfterEightDays()
    {
        var now = DateTimeOffset.UtcNow; var schedule = new ResetRefreshSchedule();
        Assert.True(schedule.ShouldRefresh(new(now, "Live", new(100, 300, now), null), now));
        Assert.False(schedule.ShouldRefresh(UsageSnapshot.Unavailable(now), now.AddDays(9)));
    }

    [Theory]
    [InlineData(1950, 100, 96)] // Gap above an offset secondary monitor.
    [InlineData(1800, 1020, 96)] // Primary taskbar.
    [InlineData(-1800, 100, 144)] // Disconnected screen / scaled widget.
    [InlineData(3100, 900, 192)] // Scaled widget at the right edge.
    public void RecoveredWidgetFitsOneActualWorkArea(double x, double y, double size)
    {
        LayoutRect[] areas = [new(0, 0, 1920, 1040), new(1920, 400, 1280, 680)];
        var result = WidgetPlacement.InsideNearestWorkArea(new(x, y, size, size), areas);
        Assert.Contains(areas, area => result.Left >= area.Left && result.Right <= area.Right && result.Top >= area.Top && result.Bottom <= area.Bottom);
    }

    [Fact] public void ValidPositionOnNegativeOriginMonitorDoesNotMove()
    {
        var widget = new LayoutRect(-1400, 200, 144, 144);
        Assert.Equal(widget, WidgetPlacement.InsideNearestWorkArea(widget, [new(-1600, -200, 1600, 1100), new(0, 0, 1920, 1040)]));
    }

    [Fact] public async Task LockedDatabaseDoesNotBlockCallerAndQueuedClearWins()
    {
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "usage.db"); var store = new AsyncUsageRepository(path);
            await store.ReadAsync();
            using var connection = new SqliteConnection($"Data Source={path}"); connection.Open();
            using var command = connection.CreateCommand(); command.CommandText = "BEGIN IMMEDIATE"; command.ExecuteNonQuery();
            var now = DateTimeOffset.UtcNow;
            var save = store.SaveAndReadAsync(new(now, "Live", new(20, 300, now.AddHours(2)), null));
            await Task.Delay(100);
            Assert.False(save.IsCompleted); // The caller continued while SQLite waited on a lock.
            using var canceled = new CancellationTokenSource();
            var read = store.ReadAsync(canceled.Token); canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
            var clear = store.ClearAsync();
            command.CommandText = "ROLLBACK"; command.ExecuteNonQuery();
            Assert.Single(await save.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Empty(await clear.WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Empty(await store.ReadAsync());
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(directory, true); }
    }
}
