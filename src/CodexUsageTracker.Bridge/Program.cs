using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageTracker", "MobileBridge");
Directory.CreateDirectory(root);
var configPath = Path.Combine(root, "pairing.json");
if (args.Contains("--revoke"))
{
    if (File.Exists(configPath)) File.Delete(configPath);
    Console.WriteLine("Pairing revoked. Restart the bridge and pair the phone again to resume sharing.");
    return;
}
var config = File.Exists(configPath) ? JsonSerializer.Deserialize<BridgeConfiguration>(File.ReadAllText(configPath))! : new BridgeConfiguration(Guid.NewGuid().ToString("N"), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_'));
if (!File.Exists(configPath)) File.WriteAllText(configPath, JsonSerializer.Serialize(config));
var certificatePath = Path.Combine(root, "bridge.pfx");
if (!File.Exists(certificatePath))
{
    using var key = RSA.Create(2048);
    var request = new CertificateRequest("CN=AIQuotaBridge", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
    request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
    using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(2));
    File.WriteAllBytes(certificatePath, generated.Export(X509ContentType.Pfx));
}
using var certificate = new X509Certificate2(certificatePath);
string Option(string name, string fallback) { var index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
var port = int.Parse(Option("--port", "48763"));
if (port is < 1024 or > 65535) throw new ArgumentException("Port must be between 1024 and 65535.");
var allowLan = args.Contains("--allow-lan");
var privateAddress = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
    .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address)
    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && Private(a));
var host = Option("--host", allowLan ? privateAddress?.ToString() ?? "127.0.0.1" : "127.0.0.1");
if (!IPAddress.TryParse(host, out var hostAddress) || !Private(hostAddress)) throw new ArgumentException("Use a private IPv4 host address.");
var alias = Option("--alias", "Windows profile");
var pairing = new { host, port, pin = Convert.ToHexString(SHA256.HashData(certificate.RawData)), token = config.Token };
var code = "aqm1:" + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(pairing)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
// The code grants read access to quota metadata. Never log it to telemetry or git.
Console.WriteLine("AI Quota bridge · read-only Codex monitoring");
Console.WriteLine(allowLan ? "LAN listening explicitly enabled. Keep this code private; use only on a trusted private network." : "Loopback only. For a real phone, restart with --allow-lan --host <PC Wi-Fi IPv4>.");
Console.WriteLine("Pairing code (paste into Android Connections):\n" + code);
var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageTracker", "usage.db");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = Array.Empty<string>() });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 4096;
    options.Listen(allowLan ? IPAddress.Any : IPAddress.Loopback, port, listen => listen.UseHttps(certificate));
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    var remote = context.Connection.RemoteIpAddress;
    if (remote is null || !Private(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote)) { context.Response.StatusCode = 403; return; }
    try
    {
        var active = JsonSerializer.Deserialize<BridgeConfiguration>(await File.ReadAllTextAsync(configPath));
        if (active?.Token != config.Token) { context.Response.StatusCode = 401; return; }
    }
    catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
    { context.Response.StatusCode = 401; return; }
    var provided = context.Request.Headers.Authorization.ToString();
    var expected = "Bearer " + config.Token;
    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected))) { context.Response.StatusCode = 401; return; }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});
app.MapGet("/v1/status", () =>
{
    var samples = ReadHistory(databasePath);
    var latest = samples.LastOrDefault();
    object? Window(string id, int duration, double? used, long? reset) => used is null ? null : new { id, durationMinutes = duration, usedPercent = used, resetsAt = reset };
    var windows = latest is null ? Array.Empty<object?>() : new[] { Window("fiveHour", 300, latest.FiveUsed, latest.FiveReset), Window("weekly", 10080, latest.WeekUsed, latest.WeekReset) }.Where(w => w is not null).ToArray();
    // Bound response size by time bins, while keeping bucket minima/maxima and
    // chronological endpoints. No session files or chat content are read.
    var history = samples.GroupBy(s => s.Time / 1800).SelectMany(g => new[] { g.First(), g.MinBy(s => s.FiveUsed ?? 0)!, g.MaxBy(s => s.FiveUsed ?? 0)!, g.Last() }).Distinct().OrderBy(s => s.Time)
        .Select(s => new { time = s.Time, fiveRemaining = s.FiveUsed is { } five ? 100 - five : (double?)null, weekRemaining = s.WeekUsed is { } week ? 100 - week : (double?)null, fiveReset = s.FiveReset, weekReset = s.WeekReset });
    return Results.Json(new { schemaVersion = 1, collectorId = config.Id, generatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), account = new { id = config.Id + ":codex", name = alias, product = "codex", source = latest?.Source ?? "No desktop readings", observedAt = latest?.Time ?? 0, windows, history } });
});
app.Lifetime.ApplicationStarted.Register(() => Console.WriteLine("Ready. Keep the desktop tracker running; this bridge reads its quota database without changing it. Ctrl+C stops sharing."));
await app.RunAsync();

static bool Private(IPAddress address)
{
    if (address.AddressFamily != AddressFamily.InterNetwork) return false;
    var b = address.GetAddressBytes();
    return b[0] is 10 or 127 || b[0] == 192 && b[1] == 168 || b[0] == 172 && b[1] is >= 16 and <= 31;
}
static List<Sample> ReadHistory(string path)
{
    var result = new List<Sample>();
    if (!File.Exists(path)) return result;
    using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, DefaultTimeout = 2 }.ToString());
    connection.Open(); using var command = connection.CreateCommand();
    command.CommandText = "SELECT observed, source, five_used, five_reset, week_used, week_reset FROM samples WHERE observed >= $from AND observed <= $now ORDER BY observed, source";
    command.Parameters.AddWithValue("$from", DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds()); command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());
    using var reader = command.ExecuteReader();
    while (reader.Read()) result.Add(new(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDouble(2), reader.IsDBNull(3) ? null : reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetDouble(4), reader.IsDBNull(5) ? null : reader.GetInt64(5)));
    return result;
}
sealed record BridgeConfiguration(string Id, string Token);
sealed record Sample(long Time, string Source, double? FiveUsed, long? FiveReset, double? WeekUsed, long? WeekReset);
