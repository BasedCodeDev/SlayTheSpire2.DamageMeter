namespace Based.Spires2DamageMeter;
public sealed record Share(string Owner, string Source, decimal Amount);
public sealed record ResolvedDamage(object Identity, string Kind, decimal Hp, decimal Block, IReadOnlyList<Share> Shares)
{
    public SourceIdentity? Source { get; init; }
}

/// <summary>Proportional accounting convention; never modifies game stacks.</summary>
public sealed class StatusOwnershipLedger
{
    public const string Unknown = "Unattributed";
    internal readonly Dictionary<object, List<Share>> entries = new();
    public void Clear() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("StatusOwnershipLedger.Clear", this);
    public IReadOnlyList<Share> Snapshot(object effect, decimal observed) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<Share>>("StatusOwnershipLedger.Snapshot", this, effect, observed);
    public void Change(object effect, decimal before, decimal after, string? owner, string? source) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("StatusOwnershipLedger.Change", this, effect, before, after, owner, source);
    public void Transfer(object from, decimal fromAmount, object to, decimal toAmount, decimal amount) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("StatusOwnershipLedger.Transfer", this, from, fromAmount, to, toAmount, amount);
    internal void Reconcile(object effect, decimal observed) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("StatusOwnershipLedger.Reconcile", this, effect, observed);
    public static IReadOnlyList<Share> Allocate(IReadOnlyList<Share> shares, decimal amount) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<Share>>("StatusOwnershipLedger.Allocate", shares, amount);
}
