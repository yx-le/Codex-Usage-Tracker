namespace CodexUsageTracker.Core;

// SQLite's synchronous work, including initialization, stays on a worker. A single
// gate orders saves, reads and clears so a queued old read cannot resurrect history.
public sealed class AsyncUsageRepository(string path)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private UsageRepository? repository;

    public Task<IReadOnlyList<UsageSnapshot>> ReadAsync(CancellationToken token = default) =>
        RunAsync(store => store.ReadHistory(DateTimeOffset.UtcNow), token);

    public Task<IReadOnlyList<UsageSnapshot>> SaveAndReadAsync(UsageSnapshot snapshot, CancellationToken token = default) =>
        RunAsync(store => { store.Save(snapshot); store.Prune(DateTimeOffset.UtcNow); return store.ReadHistory(DateTimeOffset.UtcNow); }, token);

    public Task<IReadOnlyList<UsageSnapshot>> ClearAsync(CancellationToken token = default) =>
        RunAsync(store => { store.Clear(); return store.ReadHistory(DateTimeOffset.UtcNow); }, token);

    private async Task<IReadOnlyList<UsageSnapshot>> RunAsync(Func<UsageRepository, IReadOnlyList<UsageSnapshot>> action, CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                repository ??= new UsageRepository(path);
                return action(repository);
            }, token).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
