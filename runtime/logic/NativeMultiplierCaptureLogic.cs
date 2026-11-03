using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.NativeMultiplierCapture;

namespace Based.Spires2DamageMeter.Runtime;
internal static class NativeMultiplierCaptureLogic
{
    public static void Postfix(AbstractModel __instance, MethodBase __originalMethod, decimal __result, object? __state) => NativeDamageCapture.ModifierPostfix(__instance, __originalMethod, __result, __state);
    public static void Prefix(AbstractModel __instance, MethodBase __originalMethod, object[] __args, out object? __state)
    {
        __state = null;
        var handler = ModifierDispatch.Select(__instance.GetType(), __originalMethod);
        if (handler == ModifierHandler.Unsupported)
        {
            NativeDamageCapture.MarkUnsupported("handler=Chain; model=" + __instance.GetType().FullName + "; unsupported method=" + __originalMethod);
            return;
        }

        if (handler != ModifierHandler.Chain)
            return;
        var f = new Frame
        {
            Parent = current.Value,
            Root = Guid.NewGuid().ToString("N"),
            Method = __originalMethod.DeclaringType?.FullName + "." + __originalMethod,
            Weak = __instance is WeakPower
        };
        __state = f;
        current.Value = f;
        try
        {
            var p = (PowerModel)__instance;
            var target = __args[0] as Creature;
            var dealer = __args[3] as Creature;
            f.Reason = $"targetOwner={ReferenceEquals(target, p.Owner)};dealerOwner={ReferenceEquals(dealer, p.Owner)};powered={((ValueProp)__args[2]).IsPoweredAttack()}";
            f.Eligible = MultiplierChain.RootEligible(!f.Weak, ReferenceEquals(target, p.Owner), ReferenceEquals(dealer, p.Owner), ((ValueProp)__args[2]).IsPoweredAttack());
            f.Seed = f.Eligible ? Dynamic(p, f.Weak ? "DamageDecrease" : "DamageIncrease") : 1;
            if (!f.Eligible)
                return;
            if (f.Weak)
            {
                Add(f, target?.Player?.GetRelic<PaperKrane>());
                Add(f, dealer?.GetPower<DebilitatePower>());
            }
            else
            {
                Add(f, dealer?.Player?.GetRelic<PaperPhrog>());
                Add(f, dealer?.GetPower<CrueltyPower>());
                Add(f, target?.GetPower<DebilitatePower>());
            }
        }
        catch (Exception e)
        {
            f.Valid = false;
            NativeDamageCapture.MarkUnsupported("handler=Chain; method=" + f.Method + "; snapshot: " + e.Message);
        }
    }

    public static void Add(Frame f, AbstractModel? m)
    {
        if (m != null)
            f.Expected.Add(m);
    }

    public static decimal Dynamic(PowerModel p, string key)
    {
        var vars = Probe.Get(p, "DynamicVars")!;
        var item = vars.GetType().GetProperty("Item", new[] { typeof(string) })!.GetValue(vars, new object[] { key });
        return Convert.ToDecimal(Probe.Get(item, "BaseValue") ?? throw new InvalidOperationException("Missing multiplier seed"));
    }

    public static MultiplierChain? Finish(object? state, decimal output)
    {
        if (state is not Frame f)
            return null;
        current.Value = f.Parent;
        if (!f.Valid)
            return null;
        var chain = new MultiplierChain(f.Root, f.Eligible, f.Seed, f.Steps)
        {
            EligibilityReason = f.Reason,
            Family = f.Weak ? "Weak" : "Vulnerable"
        };
        if (!chain.Reconciles(output) || !f.Expected.SequenceEqual(f.Seen, ReferenceEqualityComparer.Instance))
            NativeDamageCapture.MarkUnsupported("handler=Chain; method=" + f.Method + "; callback/order mismatch");
        return chain;
    }

    public static Exception? Finalizer(Exception? __exception, object? __state)
    {
        if (__state is Frame f)
            current.Value = f.Parent;
        return __exception;
    }

    public static void ChildPrefix(AbstractModel __instance, MethodInfo __originalMethod, object[] __args, out object? __state)
    {
        __state = null;
        var f = current.Value;
        if (f == null || !f.Valid || !NativeDamageCapture.Capturing)
            return;
        try
        {
            var expected = f.Weak ? "ModifyWeakMultiplier" : "ModifyVulnerableMultiplier";
            var shape = new[]
            {
                "MegaCrit.Sts2.Core.Entities.Creatures.Creature",
                "System.Decimal",
                "MegaCrit.Sts2.Core.ValueProps.ValueProp",
                "MegaCrit.Sts2.Core.Entities.Creatures.Creature",
                "MegaCrit.Sts2.Core.Models.CardModel"
            };
            if (__originalMethod.Name != expected || __originalMethod.ReturnType != typeof(decimal) || !__originalMethod.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(shape) || !ModifierContracts.Supported(__originalMethod))
                throw new InvalidOperationException("Unexpected child family/signature: " + __originalMethod);
            var owner = __instance is PowerModel p ? p.Owner : (__instance as RelicModel)?.Owner.Creature;
            bool eligible = MultiplierChain.ChildEligible(__instance.GetType().Name, f.Weak, ReferenceEquals(__args[0], owner), ReferenceEquals(__args[3], owner), ((ValueProp)__args[2]).IsPoweredAttack());
            var value = __instance is CrueltyPower cruelty ? cruelty.Amount / 100m : __instance is PaperPhrog ? .25m : __instance is PaperKrane ? -.15m : 0;
            var op = __instance is DebilitatePower ? MultiplierOperation.DoubleDeviation : MultiplierOperation.Add;
            var shares = __instance is PowerModel power ? Probe.PowerOwnership(power) : Array.Empty<Share>();
            __state = new Child(f, __instance, new(f.Root + ":" + f.Steps.Count, __instance.GetType().Name, op, value, __instance is PowerModel, eligible, shares, (decimal)__args[1], 0) { EligibilityReason = $"targetOwner={ReferenceEquals(__args[0], owner)};dealerOwner={ReferenceEquals(__args[3], owner)};powered={((ValueProp)__args[2]).IsPoweredAttack()}" });
        }
        catch (Exception e)
        {
            f.Valid = false;
            NativeDamageCapture.MarkUnsupported(e.Message);
        }
    }

    public static void ChildPostfix(decimal __result, object? __state)
    {
        if (__state is Child c)
        {
            c.Frame.Steps.Add(c.Step with { Output = __result });
            c.Frame.Seen.Add(c.Model);
        }
    }
}
