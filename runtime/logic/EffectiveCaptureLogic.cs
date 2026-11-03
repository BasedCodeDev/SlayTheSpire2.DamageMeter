using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.EffectiveCapture;

namespace Based.Spires2DamageMeter.Runtime;
internal static class EffectiveCaptureLogic
{
    public static string? Key(Creature c) => Probe.Get(c, "CombatId")?.ToString();
    public static void Before(Creature __instance, out int __state) => __state = __instance.Block;
    public static void Gained(Creature __instance, int __state)
    {
        try
        {
            if (__instance.Side.ToString() == "Enemy" && __state == 0 && __instance.Block > 0 && Key(__instance)is string id)
            {
                Probe.Effective.Discard(id);
                Probe.Emit("effective_shield_started", new { target = id });
            }
        }
        catch
        {
            Probe.Effective.Incomplete = true;
        }
    }

    public static void AfterClearCompleted(Creature c, int before)
    {
        try
        {
            if (c.Side.ToString() == "Enemy" && before > 0 && c.Block == 0 && Key(c)is string id)
            {
                Probe.Effective.Discard(id);
                Probe.Emit("effective_shield_expired", new { target = id });
            }
        }
        catch
        {
            Probe.Effective.Incomplete = true;
        }
    }
}
