using System.Text.Json;

namespace CodexUsageTracker.Core;

public static class RpcResponseParser
{
    public static bool TryReadResult(JsonElement root, int expectedId, out JsonElement result)
    {
        result = default;
        if (root.ValueKind != JsonValueKind.Object) throw new IOException("Invalid Codex response.");
        if (!root.TryGetProperty("id", out var id)) return false; // Notification.
        if (id.ValueKind != JsonValueKind.Number || !id.TryGetInt32(out var value) || value != expectedId) return false;
        if (root.TryGetProperty("error", out _) || !root.TryGetProperty("result", out var payload))
            throw new IOException("Codex did not return quota data. Check your Codex sign-in.");
        result = payload.Clone();
        return true;
    }
}
