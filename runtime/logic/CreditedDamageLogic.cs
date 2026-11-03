using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.CreditedDamage;

namespace Based.Spires2DamageMeter.Runtime;
internal static class CreditedDamageLogic
{
    public static void Clear(CreditedDamage self)
    {
        self.seen.Clear();
        self.Credits.Clear();
        self.Provisional = 0;
    }

    public static AttributionSnapshot Snapshot(CreditedDamage self) => new(CreditedDamage.Version, self.Credits.ToList(), self.Provisional);
    public static void Merge(CreditedDamage self, AttributionSnapshot state)
    {
        if (state.Version != CreditedDamage.Version)
            return;
        self.Credits.AddRange(state.Credits);
        self.Provisional += state.Provisional;
    }

    public static IReadOnlyList<DamageCredit> Add(CreditedDamage self, string id, string kind, decimal hp, decimal block, IReadOnlyList<Share> owners, SourceIdentity? trigger, AmplificationResult? amplification, bool confirmed)
    {
        if (owners.Sum(s => s.Amount) != hp)
            throw new InvalidOperationException("Raw ownership mismatch");
        var ah = amplification?.OrangeHp ?? 0;
        var ab = amplification?.OrangeBlock ?? 0;
        if (ah > hp || ab > block || ah < 0 || ab < 0)
            throw new InvalidOperationException("Credit exceeds actual damage");
        if (self.seen.Contains(id))
            return Array.Empty<DamageCredit>();
        if (!confirmed || owners.Any(s => s.Owner == StatusOwnershipLedger.Unknown) || amplification?.HpProviders.Any(s => s.Owner == StatusOwnershipLedger.Unknown) == true || amplification?.BlockProviders.Any(s => s.Owner == StatusOwnershipLedger.Unknown) == true)
            self.Provisional++;
        var parts = new List<DamageCredit>();
        foreach (var share in StatusOwnershipLedger.Allocate(owners, hp - ah))
            parts.Add(new(id, share.Owner, kind, trigger ?? new(kind, share.Source, share.Source), trigger, share.Amount, 0, false));
        if (block - ab > 0)
            parts.Add(new(id, owners.FirstOrDefault()?.Owner ?? StatusOwnershipLedger.Unknown, kind, trigger ?? new(kind, "unknown", kind), trigger, 0, block - ab, false));
        if (amplification != null)
        {
            foreach (var share in amplification.HpProviders.Where(s => s.Amount > 0))
                parts.Add(new(id, share.Owner, kind, new(kind, share.Source, share.Source + " (amplification)"), trigger, share.Amount, 0, true));
            foreach (var share in amplification.BlockProviders.Where(s => s.Amount > 0))
                parts.Add(new(id, share.Owner, kind, new(kind, share.Source, share.Source + " (amplification)"), trigger, 0, share.Amount, true));
        }

        if (parts.Sum(p => p.Hp) != hp || parts.Sum(p => p.Block) != block)
            throw new InvalidOperationException("Credited damage does not conserve actual totals");
        self.seen.Add(id);
        self.Credits.AddRange(parts);
        return parts;
    }

    public static Dictionary<string, Totals> Rows(CreditedDamage self, IEnumerable<SupportTransfer>? transfers)
    {
        var rows = new Dictionary<string, Totals>();
        foreach (var c in self.Credits)
        {
            if (!rows.TryGetValue(c.Player, out var t))
                rows[c.Player] = t = new();
            t.Hp += c.Hp;
            t.Block += c.Block;
            if (c.Kind == "Poison")
                t.Poison += c.Hp;
            if (c.Kind == "Doom")
                t.Doom += c.Hp;
            if (c.Amplification)
            {
                t.AmplifiedHp += c.Hp;
                t.AmplifiedBlock += c.Block;
            }
        }

        foreach (var transfer in transfers ?? Array.Empty<SupportTransfer>())
        {
            if (!rows.TryGetValue(transfer.Provider, out var p))
                rows[transfer.Provider] = p = new();
            if (!rows.TryGetValue(transfer.Recipient, out var r))
                rows[transfer.Recipient] = r = new();
            p.SupportGiven += transfer.Hp + transfer.Block;
            p.SupportGivenHp += transfer.Hp;
            p.SupportGivenBlock += transfer.Block;
            r.SupportReceived += transfer.Hp + transfer.Block;
            r.SupportReceivedHp += transfer.Hp;
            r.SupportReceivedBlock += transfer.Block;
        }

        return rows;
    }

    public static List<SourceTotals> Sources(CreditedDamage self) => self.Credits.GroupBy(c => (c.Player, c.Source)).Select(g => new SourceTotals { Player = g.Key.Player, Source = g.Key.Source, Hp = g.Sum(c => c.Hp), Block = g.Sum(c => c.Block), AmplifiedHp = g.Where(c => c.Amplification).Sum(c => c.Hp), AmplifiedBlock = g.Where(c => c.Amplification).Sum(c => c.Block) }).ToList();
}
