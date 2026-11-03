using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.Accounting;

namespace Based.Spires2DamageMeter.Runtime;
internal static class AccountingLogic
{
    public static SourceTotals SourceRow(Accounting self, string player, SourceIdentity source)
    {
        var row = self.StructuredSources.FirstOrDefault(s => s.Player == player && s.Source == source);
        if (row == null)
        {
            row = new()
            {
                Player = player,
                Source = source
            };
            self.StructuredSources.Add(row);
        }

        return row;
    }

    public static void Reset(Accounting self)
    {
        self.seen.Clear();
        self.Rows.Clear();
        self.Sources.Clear();
        self.Transfers.Clear();
        self.StructuredSources.Clear();
    }

    public static void ApplyAmplification(Accounting self, string dealer, AmplificationResult result, SourceIdentity? source)
    {
        if (!self.Rows.TryGetValue(dealer, out var row))
            self.Rows[dealer] = row = new();
        var dealt = self.SourceRow(dealer, source ?? new("unknown", "unknown", "Unknown source"));
        dealt.AmplifiedHp += result.OrangeHp;
        dealt.AmplifiedBlock += result.OrangeBlock;
        row.AmplifiedHp += result.OrangeHp;
        row.AmplifiedBlock += result.OrangeBlock;
        void Transfer(IReadOnlyList<Share> allocations, bool hp)
        {
            foreach (var share in allocations.Where(s => s.Owner != dealer && s.Amount > 0))
            {
                if (!self.Rows.TryGetValue(share.Owner, out var provider))
                    self.Rows[share.Owner] = provider = new();
                provider.SupportGiven += share.Amount;
                row.SupportReceived += share.Amount;
                var provided = self.SourceRow(share.Owner, new("power", share.Source, share.Source));
                if (hp)
                {
                    provided.SupportGivenHp += share.Amount;
                    dealt.SupportReceivedHp += share.Amount;
                }
                else
                {
                    provided.SupportGivenBlock += share.Amount;
                    dealt.SupportReceivedBlock += share.Amount;
                }

                if (hp)
                {
                    provider.SupportGivenHp += share.Amount;
                    row.SupportReceivedHp += share.Amount;
                }
                else
                {
                    provider.SupportGivenBlock += share.Amount;
                    row.SupportReceivedBlock += share.Amount;
                }

                self.Transfers.Add(new(share.Owner, dealer, share.Source, hp ? share.Amount : 0, hp ? 0 : share.Amount));
            }
        }

        Transfer(result.HpProviders, true);
        Transfer(result.BlockProviders, false);
    }

    public static void ApplySharedAmplification(Accounting self, string kind, IReadOnlyList<Share> damageShares, AmplificationResult result)
    {
        // Status damage belongs to stack providers, which may differ from the damage command's null dealer.
        foreach (var share in StatusOwnershipLedger.Allocate(damageShares, result.OrangeHp))
        {
            var allocated = StatusOwnershipLedger.Allocate(result.HpProviders, share.Amount);
            var partial = new AmplificationResult(new(share.Amount, 0), new(0, 0), allocated, Array.Empty<Share>());
            self.ApplyAmplification(share.Owner, partial, new(kind, share.Source, share.Source));
        }
    }

    public static bool AddResolved(Accounting self, ResolvedDamage damage)
    {
        if (damage.Shares.Sum(s => s.Amount) != damage.Hp)
            throw new InvalidOperationException("Damage allocation does not reconcile");
        if (!self.seen.Add(damage.Identity))
            return false;
        foreach (var s in damage.Shares)
        {
            if (!self.Rows.TryGetValue(s.Owner, out var row))
                self.Rows[s.Owner] = row = new();
            row.Hp += s.Amount;
            self.SourceRow(s.Owner, damage.Source ?? new(damage.Kind, s.Source, s.Source)).Hp += s.Amount;
            if (damage.Kind == "Poison")
                row.Poison += s.Amount;
            if (damage.Kind == "Doom")
                row.Doom += s.Amount;
            if (!self.Sources.TryGetValue(s.Owner, out var sources))
                self.Sources[s.Owner] = sources = new();
            var key = damage.Kind + ": " + s.Source;
            sources[key] = sources.GetValueOrDefault(key) + s.Amount;
        }

        if (damage.Block > 0)
        {
            var owner = damage.Shares.FirstOrDefault()?.Owner ?? StatusOwnershipLedger.Unknown;
            if (!self.Rows.TryGetValue(owner, out var row))
                self.Rows[owner] = row = new();
            row.Block += damage.Block;
            self.SourceRow(owner, damage.Source ?? new(damage.Kind, damage.Shares.FirstOrDefault()?.Source ?? "unknown", damage.Kind)).Block += damage.Block;
        }

        return true;
    }

    public static bool Add(Accounting self, object identity, string owner, decimal unblocked, decimal blocked, decimal overkill, string kind)
    {
        var normalized = EngineDamage.Normalize(unblocked, blocked, overkill);
        return self.AddResolved(new(identity, kind, normalized.Hp, normalized.Block, new[] { new Share(owner, "Unknown source", normalized.Hp) }));
    }
}
