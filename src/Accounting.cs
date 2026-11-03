namespace Based.Spires2DamageMeter;
public sealed class Totals
{
    public decimal Hp { get; set; }
    public decimal Block { get; set; }
    public decimal Poison { get; set; }
    public decimal Doom { get; set; }
    public decimal AmplifiedHp { get; set; }
    public decimal AmplifiedBlock { get; set; }
    public decimal SupportGiven { get; set; }
    public decimal SupportReceived { get; set; }
    public decimal SupportGivenHp { get; set; }
    public decimal SupportGivenBlock { get; set; }
    public decimal SupportReceivedHp { get; set; }
    public decimal SupportReceivedBlock { get; set; }
}

public sealed record SupportTransfer(string Provider, string Recipient, string Source, decimal Hp, decimal Block);
public sealed class Accounting
{
    internal readonly HashSet<object> seen = new(ReferenceEqualityComparer.Instance);
    public Dictionary<string, Totals> Rows { get; } = new();
    public Dictionary<string, Dictionary<string, decimal>> Sources { get; } = new();
    public List<SupportTransfer> Transfers { get; } = new();
    public List<SourceTotals> StructuredSources { get; } = new();

    internal SourceTotals SourceRow(string player, SourceIdentity source) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<SourceTotals>("Accounting.SourceRow", this, player, source);
    public void Reset() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Accounting.Reset", this);
    public void ApplyAmplification(string dealer, AmplificationResult result, SourceIdentity? source = null) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Accounting.ApplyAmplification", this, dealer, result, source);
    public void ApplySharedAmplification(string kind, IReadOnlyList<Share> damageShares, AmplificationResult result) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Accounting.ApplySharedAmplification", this, kind, damageShares, result);
    public bool AddResolved(ResolvedDamage damage) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool>("Accounting.AddResolved", this, damage);
    public bool Add(object identity, string owner, decimal unblocked, decimal blocked, decimal overkill, string kind) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool>("Accounting.Add", this, identity, owner, unblocked, blocked, overkill, kind);
}
