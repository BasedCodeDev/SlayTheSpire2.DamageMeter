using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.MultiplierChain;

namespace Based.Spires2DamageMeter.Runtime;
internal static class MultiplierChainLogic
{
    public static decimal Evaluate(MultiplierChain self, ISet<string>? excluded)
    {
        if (!self.Eligible)
            return 1;
        var value = excluded?.Contains(self.Root) == true ? 1 : self.Seed;
        foreach (var s in self.Steps)
            if (excluded?.Contains(s.Id) != true)
                value = s.Apply(value);
        return value;
    }

    public static bool Reconciles(MultiplierChain self, decimal output)
    {
        var allowed = self.Family == "Weak" ? new[]
        {
            "PaperKrane",
            "DebilitatePower"
        }

        : new[]
        {
            "PaperPhrog",
            "CrueltyPower",
            "DebilitatePower"
        };
        var last = -1;
        var value = self.Eligible ? self.Seed : 1;
        foreach (var s in self.Steps)
        {
            var rank = Array.IndexOf(allowed, s.Model);
            if (rank <= last || s.Input != value || s.Apply(value) != s.Output)
                return false;
            last = rank;
            value = s.Output;
        }

        return value == output && (self.Eligible || self.Steps.Count == 0);
    }

    public static bool RootEligible(bool vulnerable, bool targetOwner, bool dealerOwner, bool powered) => powered && (vulnerable ? targetOwner : dealerOwner);
    public static bool ChildEligible(string model, bool weak, bool targetOwner, bool dealerOwner, bool powered) => powered && (model switch
    {
        "CrueltyPower" or "PaperPhrog" => !targetOwner,
        "PaperKrane" => targetOwner,
        "DebilitatePower" => weak ? dealerOwner : targetOwner,
        _ => throw new ArgumentException("Unknown child")});
}
