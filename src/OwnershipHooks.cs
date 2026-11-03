using System.Reflection;
using HarmonyLib;
namespace Based.Spires2DamageMeter;

public sealed record PatchSpec(MethodInfo Original, HarmonyMethod? Prefix = null, HarmonyMethod? Postfix = null, HarmonyMethod? Finalizer = null);

public static class OwnershipHooks
{
    public static bool IsTask(Type type) => type == typeof(Task) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>));
    public static MethodInfo Resolve(Type type, string name, params Type[] parameters)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, parameters)
            ?? throw new MissingMethodException($"Missing {type.FullName}.{name}({string.Join(",", parameters.Select(p => p.FullName))})");
        if (method.IsGenericMethod || !IsTask(method.ReturnType)) throw new InvalidOperationException($"Unsupported {method}: expected static Task or Task<T>");
        return method;
    }
    public static MethodInfo[] ResolveGame(Assembly game)
    {
        Type T(string name) => game.GetType("MegaCrit.Sts2.Core." + name, true)!;
        var commands = T("Commands.PowerCmd"); var power = T("Models.PowerModel"); var creature = T("Entities.Creatures.Creature"); var card = T("Models.CardModel"); var choice = T("GameActions.Multiplayer.PlayerChoiceContext");
        var hook = T("Hooks.Hook"); var combat = T("Combat.ICombatState");
        var before = Resolve(hook, "BeforePowerAmountChanged", combat, power, typeof(decimal), creature, creature, card);
        var after = Resolve(hook, "AfterPowerAmountChanged", combat, choice, power, typeof(decimal), creature, card);
        var apply = Resolve(commands, "Apply", choice, power, creature, typeof(decimal), creature, card, typeof(bool));
        var modify = Resolve(commands, "ModifyAmount", choice, power, typeof(decimal), creature, card, typeof(bool));
        foreach (var method in new[] { apply, modify })
            foreach (var name in new[] { "applier", "cardSource" })
                if (!method.GetParameters().Any(p => p.Name == name)) throw new InvalidOperationException($"Missing binding {name}: {method}");
        var remove = Resolve(commands, "Remove", power);
        if (remove.ReturnType != typeof(Task)) throw new InvalidOperationException($"Expected Task: {remove}");
        return new[] { before, after, apply, modify, remove };
    }
    public static void Install(Harmony harmony, IReadOnlyList<PatchSpec> specs, Action<MethodInfo>? installed = null)
    {
        MethodInfo? current = null;
        try
        {
            foreach (var spec in specs)
            {
                current = spec.Original;
                harmony.Patch(spec.Original, spec.Prefix, spec.Postfix, finalizer: spec.Finalizer);
                var info = Harmony.GetPatchInfo(spec.Original);
                if (info == null || (spec.Prefix != null && !info.Prefixes.Any(p => p.owner == harmony.Id)) || (spec.Postfix != null && !info.Postfixes.Any(p => p.owner == harmony.Id)) || (spec.Finalizer != null && !info.Finalizers.Any(p => p.owner == harmony.Id))) throw new InvalidOperationException($"Patch verification failed: {spec.Original}");
                installed?.Invoke(spec.Original);
            }
        }
        catch (Exception ex) { harmony.UnpatchAll(harmony.Id); throw new InvalidOperationException($"Ownership installation failed for {current?.DeclaringType?.FullName}.{current}: {ex.Message}", ex); }
    }
}
