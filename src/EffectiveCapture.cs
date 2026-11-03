using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace Based.Spires2DamageMeter;
public static class EffectiveCapture
{
    public static bool Enabled { get; internal set; }

    internal static string? Key(Creature c) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string?>("EffectiveCapture.Key", c);
    public static void Register()
    {
        var h = new Harmony(Probe.Id + ".Effective");
        try
        {
            var gain = AccessTools.Method(typeof(Creature), "GainBlockInternal", new[] { typeof(decimal) }) ?? throw new Exception("Missing block gain");
            var clear = AccessTools.Method(typeof(Creature), "ClearBlock", Type.EmptyTypes) ?? throw new Exception("Missing block clear");
            if (gain.ReturnType != typeof(void) || clear.ReturnType != typeof(Task))
                throw new Exception("Unsupported block lifecycle");
            h.Patch(gain, prefix: new(typeof(EffectiveCapture), nameof(Before)), postfix: new(typeof(EffectiveCapture), nameof(Gained)));
            h.Patch(clear, prefix: new(typeof(EffectiveCapture), nameof(Before)), postfix: new(typeof(EffectiveCapture), nameof(Cleared)));
            Enabled = true;
        }
        catch (Exception e)
        {
            h.UnpatchAll(h.Id);
            Probe.Effective.Incomplete = true;
            Probe.Emit("effective_capture_error", new { error = e.Message });
        }
    }

    public static void Before(Creature __instance, out int __state)
    {
        object? [] values = new object? []
        {
            __instance,
            null
        };
        __state = __instance.Block;
        if (Probe.GuardHook("EffectiveCapture.Before", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveCapture.Before", values)))
            __state = (int)values[1]!;
    }

    public static void Gained(Creature __instance, int __state) => Probe.GuardHook("EffectiveCapture.Gained", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveCapture.Gained", __instance, __state));
    public static void Cleared(Creature __instance, int __state, ref Task __result) => __result = AfterClear(__result, __instance, __state);
    internal static async Task AfterClear(Task task, Creature c, int before)
    {
        await task;
        AfterClearCompleted(c, before);
    }

    internal static void AfterClearCompleted(Creature c, int before) => Probe.GuardHook("EffectiveCapture.AfterClearCompleted", () => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("EffectiveCapture.AfterClearCompleted", c, before));
}
