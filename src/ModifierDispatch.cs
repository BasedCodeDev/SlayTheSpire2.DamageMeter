using System.Reflection;

namespace Based.Spires2DamageMeter;
public enum ModifierHandler
{
    Generic,
    Chain,
    Additive,
    AttackMultiplier,
    Unsupported
}

public static class ModifierDispatch
{
    public static ModifierHandler Select(Type model, MethodBase method) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<ModifierHandler>("ModifierDispatch.Select", model, method);
}
