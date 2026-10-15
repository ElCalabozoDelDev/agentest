using agentest.Models;

namespace agentest.Services;

public sealed class HealthMonitorService : IDisposable
{
    private sealed record Entry(AgentProfile Profile, string Credential);
    private readonly HealthClient client;
    private readonly object gate = new();
    private readonly Dictionary<string, AgentHealth> results = [];
    private readonly SemaphoreSlim round = new(1);
    private readonly CancellationTokenSource stopping = new();
    private readonly CancellationToken token;
    private Entry[] entries = [];
    private Task? loop;
    private bool disposed;
    private bool refreshing;
    public event EventHandler? Changed;
    public bool IsRefreshing { get { lock (gate) return refreshing; } }
    public IReadOnlyDictionary<string, AgentHealth> Results { get { lock (gate) return new Dictionary<string, AgentHealth>(results); } }

    public HealthMonitorService(HealthClient? client = null) { this.client = client ?? new(); token = stopping.Token; }

    public void UpdateProfiles(IReadOnlyList<AgentProfile> profiles, IReadOnlyDictionary<string, string> credentials)
    {
        lock (gate)
        {
            foreach (var id in results.Keys.Where(id => !profiles.Any(p => entries.Any(e => e.Profile.Id == id && ReferenceEquals(e.Profile, p) && e.Credential == credentials.GetValueOrDefault(id, "")))).ToList()) results.Remove(id);
            entries = profiles.Select(p => new Entry(p, credentials.GetValueOrDefault(p.Id, ""))).ToArray();
        }
    }

    public void Start()
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); loop ??= RunAsync(); }
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try { while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)) await RefreshAsync().ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async Task RefreshAsync(bool force = false)
    {
        if (token.IsCancellationRequested || !await round.WaitAsync(0, token).ConfigureAwait(false)) return;
        var published = false;
        try
        {
            Entry[] due;
            lock (gate)
            {
                due = entries.Where(e => !string.IsNullOrWhiteSpace(e.Profile.Health.Endpoint) &&
                    (force || !results.TryGetValue(e.Profile.Id, out var result) || (DateTimeOffset.Now - result.CheckedAt).TotalSeconds >= e.Profile.Health.IntervalSeconds)).ToArray();
                if (due.Length == 0) return;
                refreshing = true;
                published = true;
            }
            if (published) Notify();
            await Task.WhenAll(due.Select(async entry =>
            {
                var result = await client.CheckAsync(entry.Profile.Health, entry.Credential, token).ConfigureAwait(false);
                lock (gate)
                    if (!token.IsCancellationRequested && entries.Any(e => ReferenceEquals(e.Profile, entry.Profile) && e.Credential == entry.Credential)) results[entry.Profile.Id] = result;
            })).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        finally
        {
            lock (gate) refreshing = false;
            round.Release();
            if (published) Notify();
        }
    }

    private void Notify() { if (!token.IsCancellationRequested) Changed?.Invoke(this, EventArgs.Empty); }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            entries = [];
            results.Clear();
        }
        stopping.Cancel();
        client.Dispose();
        // A check can still be unwinding; release its token source after the periodic loop exits.
        if (loop is not null) _ = loop.ContinueWith(_ => stopping.Dispose(), TaskScheduler.Default);
        else stopping.Dispose();
    }
}
