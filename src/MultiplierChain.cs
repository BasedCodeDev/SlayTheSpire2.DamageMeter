namespace Based.Spires2DamageMeter;
public enum MultiplierOperation
{
    Add,
    DoubleDeviation
}

public sealed record MultiplierStep(string Id, string Model, MultiplierOperation Operation, decimal Value, bool Status, bool Eligible, IReadOnlyList<Share> Providers, decimal Input, decimal Output)
{
    public string EligibilityReason { get; init; } = "captured";

    public decimal Apply(decimal input) => !Eligible ? input : Operation == MultiplierOperation.Add ? input + Value : 2 * input - 1;
}

public sealed record MultiplierChain(string Root, bool Eligible, decimal Seed, IReadOnlyList<MultiplierStep> Steps)
{
    public string Family { get; init; } = "Vulnerable";
    public string EligibilityReason { get; init; } = "captured";

    public decimal Evaluate(ISet<string>? excluded = null) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<decimal>("MultiplierChain.Evaluate", this, excluded);
    public bool Reconciles(decimal output) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool>("MultiplierChain.Reconciles", this, output);
    public static bool RootEligible(bool vulnerable, bool targetOwner, bool dealerOwner, bool powered) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool>("MultiplierChain.RootEligible", vulnerable, targetOwner, dealerOwner, powered);
    public static bool ChildEligible(string model, bool weak, bool targetOwner, bool dealerOwner, bool powered) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool>("MultiplierChain.ChildEligible", model, weak, targetOwner, dealerOwner, powered);
}
