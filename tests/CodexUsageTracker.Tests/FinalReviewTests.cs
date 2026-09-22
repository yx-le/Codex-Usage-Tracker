using System.Text.Json;
using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public class FinalReviewTests
{
    [Theory]
    [InlineData("{\"id\":2}")]
    [InlineData("{\"id\":2,\"error\":{}}")]
    [InlineData("null")]
    public void BrokenRpcResponsesBecomeRecoverableErrors(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.Throws<IOException>(() => RpcResponseParser.TryReadResult(document.RootElement, 2, out _));
    }
    [Theory]
    [InlineData("{\"method\":\"notification\"}")]
    [InlineData("{\"id\":1}")]
    [InlineData("{\"id\":\"other\"}")]
    public void UnrelatedMessagesAreIgnored(string json)
    {
        using var document = JsonDocument.Parse(json);
        Assert.False(RpcResponseParser.TryReadResult(document.RootElement, 2, out _));
    }
    [Fact] public void ResultSurvivesDocumentDisposal()
    {
        JsonElement result;
        using (var document = JsonDocument.Parse("{\"id\":2,\"result\":{\"ok\":true}}"))
            Assert.True(RpcResponseParser.TryReadResult(document.RootElement, 2, out result));
        Assert.True(result.GetProperty("ok").GetBoolean());
    }
    [Theory]
    [InlineData(59, "Resets in less than 1 minute")]
    [InlineData(1, "Resets in less than 1 minute")]
    [InlineData(60, "Resets in 0h 1m")]
    [InlineData(0, "Reset due · awaiting update")]
    public void CountdownHandlesLastMinute(int seconds, string expected)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(expected, new QuotaWindow(20, 300, now.AddSeconds(seconds)).Countdown(now));
    }
    [Fact] public void EditingDraftDoesNotChangeActiveSettings()
    {
        var active = new TrackerSettings(); var draft = active.Copy();
        draft.Theme = "Light"; draft.WarningPercent = 44;
        Assert.Equal("System", active.Theme); Assert.Equal(25, active.WarningPercent);
    }
}
