namespace Based.Spires2DamageMeter;
public sealed record PowerChange(string OperationId, string Key, decimal Before, decimal After, string? Owner, string? Source, string Quality, IReadOnlyList<Share> Shares);
public sealed class PowerChangeTracker
{
    internal sealed record Pending(string Operation, string Key, decimal Before, string? Owner, string? Source)
    {
        public decimal Accounted { get; set; }
    }

    internal readonly List<Pending> pending = new();
    internal readonly StatusOwnershipLedger ledger = new();
    internal readonly Dictionary<string, decimal> observed = new();
    public int Confirmed { get; internal set; }
    public int Unknown { get; internal set; }
    public int Unresolved { get; internal set; }

    public void Begin(string operation, string key, decimal before, string? owner, string? source) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("PowerChangeTracker.Begin", this, operation, key, before, owner, source);
    public PowerChange End(string operation, string key, decimal after, string? owner, string? source) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<PowerChange>("PowerChangeTracker.End", this, operation, key, after, owner, source);
    public IReadOnlyList<Share> Snapshot(string key, decimal amount) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<Share>>("PowerChangeTracker.Snapshot", this, key, amount);
    public void EndScope(string operation) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("PowerChangeTracker.EndScope", this, operation);
    public void Remove(string key) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("PowerChangeTracker.Remove", this, key);
    public void Clear() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("PowerChangeTracker.Clear", this);
}
