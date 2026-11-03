using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.EffectiveDamageLedger;

namespace Based.Spires2DamageMeter.Runtime;
internal static class EffectiveDamageLedgerLogic
{
    public static void Clear(EffectiveDamageLedger self)
    {
        self.Credits.Clear();
        self.Pending.Clear();
        self.seen.Clear();
        self.Incomplete = false;
    }

    public static void Discard(EffectiveDamageLedger self, string target) => self.Pending.Remove(target);
    public static void Finish(EffectiveDamageLedger self) => self.Pending.Clear();
    public static void Add(EffectiveDamageLedger self, string id, string? target, IReadOnlyList<DamageCredit> parts, bool? breakthrough)
    {
        if (!self.seen.Add(id))
            return;
        self.Credits.AddRange(parts.Where(c => c.Hp > 0).Select(c => c with { Block = 0 }));
        if (target == null || breakthrough == null)
        {
            self.Incomplete = true;
            return;
        }

        if (!self.Pending.TryGetValue(target, out var pending))
            self.Pending[target] = pending = new();
        pending.AddRange(parts.Where(c => c.Block > 0).Select(c => c with { Hp = 0 }));
        if (breakthrough == true)
        {
            self.Credits.AddRange(pending);
            self.Pending.Remove(target);
        }
    }

    public static EffectiveSnapshot Snapshot(EffectiveDamageLedger self) => new(EffectiveDamageLedger.Version, self.Credits.ToList(), self.Pending.ToDictionary(p => p.Key, p => p.Value.ToList()), self.Incomplete);
    public static void Merge(EffectiveDamageLedger self, EffectiveSnapshot s)
    {
        if (s.Version != EffectiveDamageLedger.Version)
        {
            self.Incomplete = true;
            return;
        }

        self.Credits.AddRange(s.Credits);
        foreach (var p in s.Pending)
            self.Pending[p.Key] = p.Value.ToList();
        self.Incomplete |= s.Incomplete;
    }

    public static CreditedDamage Ledger(EffectiveDamageLedger self)
    {
        var ledger = new CreditedDamage();
        ledger.Merge(new(CreditedDamage.Version, self.Credits.ToList(), 0));
        return ledger;
    }

    public static decimal PendingFor(EffectiveDamageLedger self, string player) => self.Pending.Values.SelectMany(p => p).Where(c => c.Player == player).Sum(c => c.Block);
}
