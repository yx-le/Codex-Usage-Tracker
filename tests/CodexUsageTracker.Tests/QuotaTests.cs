using System.Text.Json;
using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public class QuotaTests
{
    private static UsageSnapshot Parse(string json) { using var doc = JsonDocument.Parse(json); return QuotaParser.Parse(doc.RootElement, DateTimeOffset.UtcNow, "Codex app-server"); }

    [Fact] public void PrefersCodexBucketOverLegacyAndOtherModels()
    {
        var sample = Parse("""{"rateLimits":{"primary":{"usedPercent":90,"windowDurationMins":300}},"rateLimitsByLimitId":{"codex":{"primary":{"usedPercent":27,"windowDurationMins":300},"secondary":{"usedPercent":58,"windowDurationMins":10080}},"other":{"primary":{"usedPercent":99,"windowDurationMins":300}}}}""");
        Assert.Equal(73, sample.FiveHour!.RemainingPercent); Assert.Equal(42, sample.Weekly!.RemainingPercent);
    }
    [Fact] public void DoesNotSubstituteModelSpecificBucket()
    {
        Assert.False(Parse("""{"rateLimitsByLimitId":{"other":{"primary":{"usedPercent":99,"windowDurationMins":300}}}}""").HasData);
        Assert.False(Parse("""{"limit_id":"other","primary":{"used_percent":9,"window_minutes":300}}""").HasData);
    }
    [Theory]
    [InlineData("{}")] [InlineData("{\"primary\":null}")]
    [InlineData("{\"primary\":{\"usedPercent\":0}}")]
    [InlineData("{\"primary\":{\"usedPercent\":0,\"windowDurationMins\":15}}")]
    [InlineData("{\"primary\":{\"usedPercent\":null,\"windowDurationMins\":300}}")]
    [InlineData("null")]
    public void MissingOrUnsupportedQuotaIsNotZeroUsage(string json) => Assert.False(Parse(json).HasData);

    [Fact] public void SupportsSnakeCaseAndNullReset()
    {
        var sample = Parse("""{"rate_limits":{"primary":{"used_percent":20,"window_minutes":300,"resets_at":null},"secondary":{"used_percent":51,"window_minutes":10080,"resets_at":1800000000}}}""");
        Assert.Equal(80, sample.FiveHour!.RemainingPercent); Assert.Null(sample.FiveHour.ResetsAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800000000), sample.Weekly!.ResetsAt);
    }
    [Theory] [InlineData(-12,100)] [InlineData(125,0)]
    public void ClampsPercent(double used, double expected) => Assert.Equal(expected, new QuotaWindow(used,300,null).RemainingPercent);

    [Fact] public void ExpiredResetDoesNotClaimRecovery()
    {
        var now = DateTimeOffset.UtcNow; var window = new QuotaWindow(100,300,now.AddSeconds(-1));
        Assert.True(window.HasExpired(now)); Assert.Contains("awaiting update",window.Countdown(now));
        Assert.Equal("Pace unavailable",PaceCalculator.Describe(window,now));
    }
    [Fact] public void PaceUsesElapsedWindowAndHandlesUnknownReset()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal("Above sustainable pace",PaceCalculator.Describe(new(70,300,now.AddMinutes(150)),now));
        Assert.Equal("On track",PaceCalculator.Describe(new(30,300,now.AddMinutes(150)),now));
        Assert.Equal("Learning pace",PaceCalculator.Describe(new(0,300,now.AddMinutes(299)),now));
        Assert.Equal("Pace unavailable",PaceCalculator.Describe(new(10,300,null),now));
    }
    [Fact] public void StalenessIncludesFutureTimestamps()
    {
        var now=DateTimeOffset.UtcNow;
        Assert.True(new UsageSnapshot(now.AddMinutes(-4),"Local",new(5,300,null),null).IsStale(now));
        Assert.True(new UsageSnapshot(now.AddHours(1),"Local",new(5,300,null),null).IsStale(now));
    }
    [Fact] public void AlertsDeduplicateAndRearmAfterReset()
    {
        var now=DateTimeOffset.UtcNow; var policy=new AlertPolicy(); var settings=new TrackerSettings();
        UsageSnapshot Sample(double used,DateTimeOffset reset) => new(now,"Codex app-server",new(used,300,reset),null);
        Assert.Single(policy.Evaluate(Sample(76,now.AddHours(1)),settings,now));
        Assert.Empty(policy.Evaluate(Sample(77,now.AddHours(1)),settings,now));
        Assert.Single(policy.Evaluate(Sample(95,now.AddHours(1)),settings,now));
        Assert.Empty(policy.Evaluate(Sample(99,now.AddHours(1)),settings,now));
        Assert.Single(policy.Evaluate(Sample(99,now.AddHours(6)),settings,now));
    }
    [Fact] public void AlertsIgnoreStaleLocalMissingAndExpiredData()
    {
        var now=DateTimeOffset.UtcNow; var policy=new AlertPolicy(); var settings=new TrackerSettings();
        var live=new UsageSnapshot(now,"Codex app-server",new(99,300,now.AddHours(1)),null);
        Assert.Empty(policy.Evaluate(live with {Source="Local Codex fallback"},settings,now));
        Assert.Empty(policy.Evaluate(live with {ObservedAt=now.AddHours(-1)},settings,now));
        Assert.Empty(policy.Evaluate(live with {FiveHour=new(99,300,now.AddSeconds(-1))},settings,now));
        Assert.Empty(policy.Evaluate(live, new TrackerSettings {AlertsEnabled=false},now));
    }
}
