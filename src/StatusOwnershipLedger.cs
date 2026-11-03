namespace Based.Spires2DamageMeter;

public sealed record Share(string Owner, string Source, decimal Amount);
public sealed record ResolvedDamage(object Identity, string Kind, decimal Hp, decimal Block, IReadOnlyList<Share> Shares)
{ public SourceIdentity? Source { get; init; } }

/// <summary>Proportional accounting convention; never modifies game stacks.</summary>
public sealed class StatusOwnershipLedger
{
    public const string Unknown = "Unattributed";
    private readonly Dictionary<object, List<Share>> entries = new();
    public void Clear() => entries.Clear();
    public IReadOnlyList<Share> Snapshot(object effect, decimal observed)
    {
        Reconcile(effect, observed);
        return entries.TryGetValue(effect, out var shares) ? shares.ToArray() : Array.Empty<Share>();
    }
    public void Change(object effect, decimal before, decimal after, string? owner, string? source)
    {
        Reconcile(effect, before);
        after = Math.Max(0, after);
        if (after == 0) { entries.Remove(effect); return; }
        var shares = entries.GetValueOrDefault(effect) ?? new List<Share>();
        if (after > before) shares.Add(new(owner ?? Unknown, source ?? "Unknown source", after - Math.Max(0, before)));
        else shares = Allocate(shares, after).ToList();
        entries[effect] = shares.GroupBy(s => (s.Owner, s.Source)).Select(g => new Share(g.Key.Owner, g.Key.Source, g.Sum(s => s.Amount))).ToList();
    }
    public void Transfer(object from, decimal fromAmount, object to, decimal toAmount, decimal amount)
    {
        var moving = Allocate(Snapshot(from, fromAmount), Math.Min(amount, fromAmount));
        Change(from, fromAmount, Math.Max(0, fromAmount - amount), null, null);
        Reconcile(to, toAmount);
        if (!entries.TryGetValue(to, out var target)) entries[to] = target = new();
        target.AddRange(moving);
    }
    private void Reconcile(object effect, decimal observed)
    {
        observed = Math.Max(0, observed);
        if (observed == 0) { entries.Remove(effect); return; }
        if (!entries.TryGetValue(effect, out var shares)) entries[effect] = shares = new();
        var known = shares.Sum(s => s.Amount);
        if (known < observed) shares.Add(new(Unknown, "Missing stack history", observed - known));
        if (known > observed) entries[effect] = Allocate(shares, observed).ToList();
    }
    public static IReadOnlyList<Share> Allocate(IReadOnlyList<Share> shares, decimal amount)
    {
        amount = Math.Max(0, amount);
        var ordered = shares.Where(s => s.Amount > 0).OrderBy(s => s.Owner, StringComparer.Ordinal).ThenBy(s => s.Source, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0) return amount > 0 ? new[] { new Share(Unknown, "Missing stack history", amount) } : Array.Empty<Share>();
        var total = ordered.Sum(s => s.Amount); var remaining = amount;
        return ordered.Select((s, i) => { var value = i == ordered.Length - 1 ? remaining : Math.Min(remaining, amount * (s.Amount / total)); remaining -= value; return s with { Amount = value }; }).ToArray();
    }
}
