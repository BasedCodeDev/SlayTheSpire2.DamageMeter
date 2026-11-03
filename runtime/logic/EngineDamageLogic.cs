using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.EngineDamage;

namespace Based.Spires2DamageMeter.Runtime;
internal static class EngineDamageLogic
{
    public static EngineDamage Normalize(decimal unblockedDamage, decimal blockedDamage, decimal overkillDamage) => new(Math.Max(0, unblockedDamage), Math.Max(0, blockedDamage), Math.Max(0, overkillDamage));
}
