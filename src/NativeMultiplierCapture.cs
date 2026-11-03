using System.Reflection;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.ValueProps;

namespace Based.Spires2DamageMeter;
public static class NativeMultiplierCapture
{
    public static void Postfix(AbstractModel __instance, MethodBase __originalMethod, decimal __result, object? __state) => Probe.GuardHook("NativeMultiplierCapture.Postfix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.Postfix", __instance, __originalMethod, __result, __state));
    internal sealed class Frame
    {
        public Frame? Parent;
        public required string Root;
        public bool Weak, Eligible;
        public bool Valid = true;
        public string Reason = "";
        public string Method = "";
        public decimal Seed;
        public List<MultiplierStep> Steps = new();
        public List<AbstractModel> Expected = new(), Seen = new();
    }

    internal sealed record Child(Frame Frame, AbstractModel Model, MultiplierStep Step);
    internal static readonly AsyncLocal<Frame?> current = new();
    public static void Prefix(AbstractModel __instance, MethodBase __originalMethod, object[] __args, out object? __state)
    {
        var previous = current.Value;
        __state = null;
        object? [] values = new object? []
        {
            __instance,
            __originalMethod,
            __args,
            null
        };
        if (Probe.GuardHook("NativeMultiplierCapture.Prefix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.Prefix", values)))
            __state = values[3];
        else current.Value = previous;
    }

    internal static void Add(Frame f, AbstractModel? m) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.Add", f, m);
    internal static decimal Dynamic(PowerModel p, string key) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<decimal>("NativeMultiplierCapture.Dynamic", p, key);
    public static MultiplierChain? Finish(object? state, decimal output) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<MultiplierChain?>("NativeMultiplierCapture.Finish", state, output);
    public static Exception? Finalizer(Exception? __exception, object? __state)
    {
        try { Probe.GuardHook("NativeMultiplierCapture.Finalizer", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.Finalizer", __exception, __state)); }
        finally { if (__state is Frame frame) current.Value = frame.Parent; }
        return __exception;
    }
    public static void ChildPrefix(AbstractModel __instance, MethodInfo __originalMethod, object[] __args, out object? __state)
    {
        object? [] values = new object? []
        {
            __instance,
            __originalMethod,
            __args,
            null
        };
        __state = null;
        if (Probe.GuardHook("NativeMultiplierCapture.ChildPrefix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.ChildPrefix", values)))
            __state = values[3];
    }

    public static void ChildPostfix(decimal __result, object? __state) => Probe.GuardHook("NativeMultiplierCapture.ChildPostfix", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeMultiplierCapture.ChildPostfix", __result, __state));
}
