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
        object? [] values = new object? []
        {
            __instance,
            __originalMethod,
            __args,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("ModifierCapturePatch.Prefix", values);
        __state = (object? )values[3]!;
    }

    public static void Postfix(AbstractModel __instance, MethodBase __originalMethod, decimal __result, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("ModifierCapturePatch.Postfix", __instance, __originalMethod, __result, __state);
    public static Exception? Finalizer(Exception? __exception, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("ModifierCapturePatch.Finalizer", __exception, __state);
}
