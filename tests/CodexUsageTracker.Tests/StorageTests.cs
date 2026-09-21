using CodexUsageTracker.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests",Guid.NewGuid().ToString("N"));
    public StorageTests() => Directory.CreateDirectory(directory);
    [Fact] public void SqliteRoundTripsOnlyQuotaAndRetainsSevenDays()
    {
        var path=Path.Combine(directory,"usage.db"); var repository=new UsageRepository(path); var now=DateTimeOffset.UtcNow;
        var snapshot=new UsageSnapshot(now,"Codex app-server",new(33,300,now.AddHours(2)),null);
        repository.Save(snapshot); repository.Save(snapshot);
        repository.Save(snapshot with {ObservedAt=now.AddDays(-8)});
        var saved=Assert.Single(repository.ReadHistory(now)); Assert.Equal(33,saved.FiveHour!.UsedPercent); Assert.Null(saved.Weekly);
        repository.Prune(now.AddDays(8)); Assert.Empty(repository.ReadHistory(now));
        using var connection=new SqliteConnection($"Data Source={path}"); connection.Open(); using var command=connection.CreateCommand();
        command.CommandText="SELECT COUNT(*) FROM samples"; Assert.Equal(0L,command.ExecuteScalar());
    }
    [Fact] public void ClearingHistoryDeletesReadings()
    {
        var repository=new UsageRepository(Path.Combine(directory,"usage.db")); var now=DateTimeOffset.UtcNow;
        repository.Save(new(now,"Codex app-server",new(30,300,null),null)); repository.Clear(); Assert.Empty(repository.ReadHistory(now));
    }
    [Fact] public void LocalFallbackIgnoresMessagesAndIncompleteRecords()
    {
        var now=DateTimeOffset.UtcNow; var sessions=Path.Combine(directory,"sessions",now.ToString("yyyy"),now.ToString("MM"),now.ToString("dd")); Directory.CreateDirectory(sessions);
        File.WriteAllLines(Path.Combine(sessions,"sample.jsonl"),new[] {
            """{"type":"response_item","payload":{"role":"user","rate_limits":{"primary":{"used_percent":99,"window_minutes":300}}}}""",
            System.Text.Json.JsonSerializer.Serialize(new { timestamp=now, type="event_msg", payload=new { type="token_count", rate_limits=new { primary=new { used_percent=35, window_minutes=300 } } } }),
            "{\"rate_limits\":" });
        var snapshot=new LocalCodexSource().Read(directory,CancellationToken.None);
        Assert.Equal(65,snapshot!.FiveHour!.RemainingPercent); Assert.Equal("Local Codex fallback",snapshot.Source);
    }
    [Fact] public void CorruptSettingsFallBackToSafeDefaults()
    {
        var path=Path.Combine(directory,"settings.json"); File.WriteAllText(path,"not json"); var settings=TrackerSettings.Load(path);
        Assert.True(settings.OnlyWhileCodexRunning); Assert.Equal(25,settings.WarningPercent); Assert.Equal(10,settings.CriticalPercent);
        settings.Theme="Dark"; settings.Save(path); Assert.Equal("Dark",TrackerSettings.Load(path).Theme);
    }
    public void Dispose() { SqliteConnection.ClearAllPools(); Directory.Delete(directory,true); }
}
