namespace Based.Spires2DamageMeter;

public sealed record PowerChange(string OperationId, string Key, decimal Before, decimal After, string? Owner, string? Source, string Quality, IReadOnlyList<Share> Shares);

public sealed class PowerChangeTracker
{
    private sealed record Pending(string Operation, string Key, decimal Before, string? Owner, string? Source)
    { public decimal Accounted { get; set; } }
    private readonly List<Pending> pending = new();
    private readonly StatusOwnershipLedger ledger = new();
    private readonly Dictionary<string, decimal> observed = new();
    public int Confirmed { get; private set; }
    public int Unknown { get; private set; }
    public int Unresolved { get; private set; }
    public void Begin(string operation, string key, decimal before, string? owner, string? source)
    { pending.Add(new(operation, key, Math.Max(0, before), owner, source)); }
    public PowerChange End(string operation, string key, decimal after, string? owner, string? source)
    {
        after = Math.Max(0, after);
        var index = pending.FindLastIndex(p => p.Operation == operation && p.Key == key);
        if (index < 0)
        {
            var quality = observed.TryGetValue(key, out var previous) && previous == after ? "duplicate" : "unmatched";
            if (quality == "unmatched") { Unresolved++; Unknown++; ledger.Change(key, observed.GetValueOrDefault(key), after, null, null); }
            observed[key] = after;
            return new(operation, key, previous, after, null, null, quality, ledger.Snapshot(key, after));
        }
        var frame = pending[index]; pending.RemoveAt(index);
        var before = Math.Max(0, frame.Before + frame.Accounted);
        var delta = after - before;
        foreach (var parent in pending.Where(p => p.Key == key)) parent.Accounted += delta;
        var provider = owner ?? frame.Owner; var application = source ?? frame.Source;
        ledger.Change(key, before, after, provider, application);
        observed[key] = after;
        if (delta != 0) { Confirmed++; if (delta > 0 && provider == null) Unknown++; }
        return new(operation, key, before, after, provider, application, delta == 0 ? "no_change" : provider == null && delta > 0 ? "unknown_provider" : "confirmed", ledger.Snapshot(key, after));
    }
    public IReadOnlyList<Share> Snapshot(string key, decimal amount) => ledger.Snapshot(key, amount);
    public void EndScope(string operation) { Unresolved += pending.RemoveAll(p => p.Operation == operation); }
    public void Remove(string key) { pending.RemoveAll(p => p.Key == key); ledger.Change(key, observed.GetValueOrDefault(key), 0, null, null); observed.Remove(key); }
    public void Clear() { pending.Clear(); observed.Clear(); ledger.Clear(); Confirmed = Unknown = Unresolved = 0; }
}
