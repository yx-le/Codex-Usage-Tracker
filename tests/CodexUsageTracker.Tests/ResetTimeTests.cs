using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class ResetTimeTests
{
    [Fact] public void ResetDateUsesRequestedTimezoneIncludingDateRollover()
    {
        var reset = new DateTimeOffset(2026, 10, 18, 23, 30, 0, TimeSpan.Zero);
        var zone = TimeZoneInfo.CreateCustomTimeZone("Test +08", TimeSpan.FromHours(8), "Test +08", "Test +08");
        var actual = new QuotaWindow(20, 300, reset).ResetDateTime(zone);
        Assert.Contains("19", actual); Assert.Contains("2026", actual); Assert.Contains("07:30", actual); Assert.Contains("UTC+08:00", actual);
        Assert.Equal("Reset date unavailable", new QuotaWindow(20, 300, null).ResetDateTime(zone));
    }
}
