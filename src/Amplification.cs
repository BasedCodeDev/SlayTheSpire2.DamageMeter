namespace Based.Spires2DamageMeter;
public sealed record AmplificationWeight(string Modifier, string Destination, decimal Weight, string Basis);
public enum ModifierKind
{
    Add,
    Multiply,
    Cap
}

public sealed record DamageModifier(string Id, ModifierKind Kind, decimal Value, bool Status, IReadOnlyList<Share> Providers)
{
    public MultiplierChain? Chain { get; init; }
}

public sealed record EffectiveDamage(decimal Hp, decimal Block);
public sealed record AmplificationResult(EffectiveDamage Actual, EffectiveDamage Baseline, IReadOnlyList<Share> HpProviders, IReadOnlyList<Share> BlockProviders)
{
    public IReadOnlyList<AmplificationWeight> Weights { get; init; } = Array.Empty<AmplificationWeight>();
    public decimal OrangeHp => HpProviders.Sum(s => s.Amount);
    public decimal OrangeBlock => BlockProviders.Sum(s => s.Amount);
    public decimal Red => Actual.Hp - OrangeHp;
    public decimal Blue => Actual.Block - OrangeBlock;
}

/// <summary>Pure replay of validated, frozen modifier outputs. Capture adapters must establish output independence from changed inputs.</summary>
public static class Amplification
{
    public const int Version = 3;
    internal static decimal Potential(decimal raw, IReadOnlyList<DamageModifier> modifiers, string? excluded = null) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<decimal>("Amplification.Potential", raw, modifiers, excluded);
    public static EffectiveDamage Resolve(decimal raw, decimal hp, decimal block, IReadOnlyList<DamageModifier> modifiers, ISet<string>? excluded = null, IReadOnlyList<HpLossRule>? hpLoss = null, bool bypassBlock = false) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<EffectiveDamage>("Amplification.Resolve", raw, hp, block, modifiers, excluded, hpLoss, bypassBlock);
    public static AmplificationResult Calculate(decimal raw, decimal hp, decimal block, IReadOnlyList<DamageModifier> modifiers, IReadOnlyList<HpLossRule>? hpLoss = null, bool bypassBlock = false) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<AmplificationResult>("Amplification.Calculate", raw, hp, block, modifiers, hpLoss, bypassBlock);
    public static Dictionary<string, decimal> SupportTransfers(string dealer, IReadOnlyList<Share> allocations) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Dictionary<string, decimal>>("Amplification.SupportTransfers", dealer, allocations);
}
