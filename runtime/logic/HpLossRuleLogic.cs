using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.HpLossRule;

namespace Based.Spires2DamageMeter.Runtime;
internal static class HpLossRuleLogic
{
    public static decimal Apply(HpLossRule self, decimal input) => self.Kind switch
    {
        HpLossRuleKind.Cap => Math.Min(input, self.Value),
        HpLossRuleKind.CapNonzero => input == 0 ? 0 : Math.Min(input, self.Value),
        HpLossRuleKind.Zero => 0,
        HpLossRuleKind.Subtract => Math.Max(0, input - self.Value),
        HpLossRuleKind.MinimumAtLeastOne => input < 1 ? input : Math.Max(input, self.Value),
        _ => throw new ArgumentOutOfRangeException()};
}
