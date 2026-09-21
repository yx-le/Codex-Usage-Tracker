using System.Diagnostics;
using System.Text.Json;

namespace CodexUsageTracker.Core;

public sealed class AppServerSource
{
    public async Task<UsageSnapshot> ReadAsync(string executable, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var token = timeout.Token;
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "-c", "analytics.enabled=false", "-c", "otel.exporter=\"none\"", "-c", "otel.trace_exporter=\"none\"", "app-server", "--listen", "stdio://" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Codex could not start.");
        // Drain diagnostics without logging or retaining potentially sensitive server output.
        var drain = DrainAsync(process.StandardError, token);
        try
        {
            await SendAsync(process, new { id = 1, method = "initialize", @params = new { clientInfo = new { name = "codex_usage_tracker", title = "Codex Usage Tracker", version = "0.1.0" } } }, token);
            await ReadResultAsync(process, 1, token);
            await SendAsync(process, new { method = "initialized" }, token);
            await SendAsync(process, new { id = 2, method = "account/rateLimits/read" }, token);
            var result = await ReadResultAsync(process, 2, token);
            return QuotaParser.Parse(result, DateTimeOffset.UtcNow, "Codex app-server");
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            timeout.Cancel();
            try { await drain; } catch (OperationCanceledException) { }
        }
    }

    private static async Task SendAsync(Process process, object value, CancellationToken token) =>
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(value).AsMemory(), token);

    private static async Task<JsonElement> ReadResultAsync(Process process, int id, CancellationToken token)
    {
        while (await process.StandardOutput.ReadLineAsync(token) is { } line)
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || !responseId.TryGetInt32(out var value) || value != id) continue;
            if (root.TryGetProperty("error", out _)) throw new IOException("Codex did not return quota data. Check your Codex sign-in.");
            return root.GetProperty("result").Clone();
        }
        throw new IOException("Codex closed the quota connection.");
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[1024];
        while (await reader.ReadAsync(buffer.AsMemory(), token) != 0) { }
    }
}
