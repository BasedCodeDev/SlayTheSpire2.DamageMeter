using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.StatusOwnershipLedger;

namespace Based.Spires2DamageMeter.Runtime;
internal static class StatusOwnershipLedgerLogic
{
    public static void Clear(StatusOwnershipLedger self) => self.entries.Clear();
    public static IReadOnlyList<Share> Snapshot(StatusOwnershipLedger self, object effect, decimal observed)
    {
        self.Reconcile(effect, observed);
        return self.entries.TryGetValue(effect, out var shares) ? shares.ToArray() : Array.Empty<Share>();
    }

    public static void Change(StatusOwnershipLedger self, object effect, decimal before, decimal after, string? owner, string? source)
    {
        self.Reconcile(effect, before);
        after = Math.Max(0, after);
        if (after == 0)
        {
            self.entries.Remove(effect);
            return;
        }

        var shares = self.entries.GetValueOrDefault(effect) ?? new List<Share>();
        if (after > before)
            shares.Add(new(owner ?? Unknown, source ?? "Unknown source", after - Math.Max(0, before)));
        else
            shares = Allocate(shares, after).ToList();
        self.entries[effect] = shares.GroupBy(s => (s.Owner, s.Source)).Select(g => new Share(g.Key.Owner, g.Key.Source, g.Sum(s => s.Amount))).ToList();
    }

    public static void Transfer(StatusOwnershipLedger self, object from, decimal fromAmount, object to, decimal toAmount, decimal amount)
    {
        var moving = Allocate(self.Snapshot(from, fromAmount), Math.Min(amount, fromAmount));
        self.Change(from, fromAmount, Math.Max(0, fromAmount - amount), null, null);
        self.Reconcile(to, toAmount);
        if (!self.entries.TryGetValue(to, out var target))
            self.entries[to] = target = new();
        target.AddRange(moving);
    }

    public static void Reconcile(StatusOwnershipLedger self, object effect, decimal observed)
    {
        observed = Math.Max(0, observed);
        if (observed == 0)
        {
            self.entries.Remove(effect);
            return;
        }

        if (!self.entries.TryGetValue(effect, out var shares))
            self.entries[effect] = shares = new();
        var known = shares.Sum(s => s.Amount);
        if (known < observed)
            shares.Add(new(Unknown, "Missing stack history", observed - known));
        if (known > observed)
            self.entries[effect] = Allocate(shares, observed).ToList();
    }

    public static IReadOnlyList<Share> Allocate(IReadOnlyList<Share> shares, decimal amount)
    {
        amount = Math.Max(0, amount);
        var ordered = shares.Where(s => s.Amount > 0).OrderBy(s => s.Owner, StringComparer.Ordinal).ThenBy(s => s.Source, StringComparer.Ordinal).ToArray();
        if (ordered.Length == 0)
            return amount > 0 ? new[]
            {
                new Share(Unknown, "Missing stack history", amount)
            }

            : Array.Empty<Share>();
        var total = ordered.Sum(s => s.Amount);
        var remaining = amount;
        return ordered.Select((s, i) =>
        {
            var value = i == ordered.Length - 1 ? remaining : Math.Min(remaining, amount * (s.Amount / total));
            remaining -= value;
            return s with
            {
                Amount = value
            };
        }).ToArray();
    }
}
