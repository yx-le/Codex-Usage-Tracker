using CodexUsageTracker.Core;
using Xunit;

namespace CodexUsageTracker.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "CodexUsageTracker.Tests",Guid.NewGuid().ToString("N"));
    public StorageTests() => Directory.CreateDirectory(directory);
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
    public void Dispose() { Directory.Delete(directory,true); }
}
