using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.Amplification;

namespace Based.Spires2DamageMeter.Runtime;
internal static class AmplificationLogic
{
    public static decimal Potential(decimal raw, IReadOnlyList<DamageModifier> modifiers, string? excluded = null)
    {
        var amount = raw;
        foreach (var m in modifiers)
        {
            if ((m.Id == excluded && m.Chain == null) || m.Kind == ModifierKind.Cap)
                continue;
            var value = m.Chain?.Evaluate(excluded == null ? null : new HashSet<string> { excluded }) ?? m.Value;
            amount = m.Kind == ModifierKind.Add ? amount + value : amount * value;
        }

        return Math.Max(0, amount);
    }

    public static EffectiveDamage Resolve(decimal raw, decimal hp, decimal block, IReadOnlyList<DamageModifier> modifiers, ISet<string>? excluded = null, IReadOnlyList<HpLossRule>? hpLoss = null, bool bypassBlock = false)
    {
        var amount = raw;
        foreach (var modifier in modifiers)
        {
            if (excluded?.Contains(modifier.Id) == true && modifier.Chain == null)
                continue;
            var value = modifier.Chain?.Evaluate(excluded) ?? modifier.Value;
            amount = modifier.Kind switch
            {
                ModifierKind.Add => amount + value,
                ModifierKind.Multiply => amount * value,
                ModifierKind.Cap => Math.Min(amount, value),
                _ => throw new ArgumentOutOfRangeException()};
        }

        amount = Math.Max(0, amount);
        var absorbed = bypassBlock ? 0 : Math.Min(Math.Max(0, block), amount);
        var lost = amount - absorbed;
        foreach (var rule in hpLoss ?? Array.Empty<HpLossRule>())
            lost = rule.Apply(lost);
        return new(Math.Min(Math.Max(0, hp), Math.Max(0, decimal.Truncate(lost))), decimal.Truncate(absorbed));
    }

    public static AmplificationResult Calculate(decimal raw, decimal hp, decimal block, IReadOnlyList<DamageModifier> modifiers, IReadOnlyList<HpLossRule>? hpLoss, bool bypassBlock)
    {
        if (modifiers.Select(m => m.Id).Distinct().Count() != modifiers.Count)
            throw new ArgumentException("Modifier occurrence IDs must be unique");
        var positive = modifiers.Where(m => m.Chain == null && m.Status && ((m.Kind == ModifierKind.Add && m.Value > 0) || (m.Kind == ModifierKind.Multiply && m.Value > 1))).Concat(modifiers.Where(m => m.Chain != null).SelectMany(m => (m.Status && m.Chain!.Eligible && m.Chain.Seed > 1 ? new[] { m with { Chain = null } } : Array.Empty<DamageModifier>()).Concat(m.Chain!.Steps.Where(s => s.Status && s.Eligible && s.Output > s.Input).Select(s => new DamageModifier(s.Id, ModifierKind.Multiply, 1, true, s.Providers))))).ToArray();
        var actual = Resolve(raw, hp, block, modifiers, hpLoss: hpLoss, bypassBlock: bypassBlock);
        var baseline = Resolve(raw, hp, block, modifiers, positive.Select(m => m.Id).ToHashSet(), hpLoss, bypassBlock);
        var diagnostics = new List<AmplificationWeight>();
        IReadOnlyList<Share> Allocate(bool health)
        {
            var budget = Math.Max(0, health ? actual.Hp - baseline.Hp : actual.Block - baseline.Block);
            var weights = new List<Share>();
            if (budget == 0)
                return Array.Empty<Share>();
            foreach (var m in positive)
            {
                var without = Resolve(raw, hp, block, modifiers, new HashSet<string> { m.Id }, hpLoss, bypassBlock);
                var marginal = Math.Max(0, health ? actual.Hp - without.Hp : actual.Block - without.Block);
                var basis = "effective_marginal";
                // Capping can make all individual removals appear redundant. Use uncapped marginals only for weights.
                if (marginal == 0)
                {
                    marginal = Math.Max(0, Potential(raw, modifiers) - Potential(raw, modifiers, m.Id));
                    basis = "uncapped_unrounded_potential";
                }

                diagnostics.Add(new(m.Id, health ? "HP" : "Block", marginal, basis));
                weights.AddRange(StatusOwnershipLedger.Allocate(m.Providers, marginal));
            }

            return StatusOwnershipLedger.Allocate(weights, budget);
        }

        return new(actual, baseline, Allocate(true), Allocate(false))
        {
            Weights = diagnostics
        };
    }

    public static Dictionary<string, decimal> SupportTransfers(string dealer, IReadOnlyList<Share> allocations)
    {
        var transfers = new Dictionary<string, decimal>();
        foreach (var share in allocations.Where(s => s.Owner != dealer && s.Amount > 0))
        {
            transfers[dealer] = transfers.GetValueOrDefault(dealer) - share.Amount;
            transfers[share.Owner] = transfers.GetValueOrDefault(share.Owner) + share.Amount;
        }

        return transfers;
    }
}
