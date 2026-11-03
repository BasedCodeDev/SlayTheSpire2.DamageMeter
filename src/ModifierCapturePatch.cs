using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace Based.Spires2DamageMeter;
public static class ModifierCapturePatch
{
    internal sealed class State
    {
        public object? Chain;
        public AdditiveSnapshot? Additive;
        public AttackMultiplierSnapshot? Multiplier;
    }

    public static void Prefix(AbstractModel __instance, MethodBase __originalMethod, object[] __args, out object? __state)
    {
        var previous = NativeMultiplierCapture.current.Value;
        __state = null;
        object? [] values = new object? []
        {
            __instance,
            __originalMethod,
            __args,
            null
        };
        if (Probe.GuardHook("ModifierCapturePatch.Prefix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("ModifierCapturePatch.Prefix", values)))
            __state = values[3];
        else NativeMultiplierCapture.current.Value = previous;
    }

    public static void Postfix(AbstractModel __instance, MethodBase __originalMethod, decimal __result, object? __state) => Probe.GuardHook("ModifierCapturePatch.Postfix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("ModifierCapturePatch.Postfix", __instance, __originalMethod, __result, __state));
    public static Exception? Finalizer(Exception? __exception, object? __state)
    {
        try { Probe.GuardHook("ModifierCapturePatch.Finalizer", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("ModifierCapturePatch.Finalizer", __exception, __state)); }
        finally { if ((__state as State)?.Chain is NativeMultiplierCapture.Frame frame) NativeMultiplierCapture.current.Value = frame.Parent; }
        return __exception;
    }
}
