using CodexUsageTracker.Core;

// Optional explicit integration check. Prints quota fields only, never raw server responses.
if (args.Length != 1) { Console.Error.WriteLine("Usage: CodexUsageTracker.Probe <path-to-codex.exe>"); return 2; }
try
{
    var snapshot = await new AppServerSource().ReadAsync(args[0], CancellationToken.None);
    Console.WriteLine($"Source: {snapshot.Source}");
    Console.WriteLine($"5-hour remaining: {snapshot.FiveHour?.RemainingPercent.ToString("0") ?? "N/A"}%");
    Console.WriteLine($"Weekly remaining: {snapshot.Weekly?.RemainingPercent.ToString("0") ?? "N/A"}%");
    Console.WriteLine($"Observed: {snapshot.ObservedAt:O}");
    return snapshot.HasData ? 0 : 1;
}
catch (Exception exception) when (exception is IOException or OperationCanceledException or System.ComponentModel.Win32Exception)
{
    Console.Error.WriteLine("Quota unavailable. Check Codex executable, sign-in, and connectivity."); return 1;
}
