using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;

namespace Based.Spires2DamageMeter;
public static class NativeDamageCapture
{
    internal sealed class Scope
    {
        public string Id = Guid.NewGuid().ToString("N");
        public List<Trace> Pending = new();
        public Trace? Active;
        public ResultRegistry<Trace> Results = new();
    }

    internal sealed class Trace
    {
        public required Creature Target;
        public decimal Raw, Hp, Block, Output;
        public List<DamageModifier> Modifiers = new();
        public List<HpLossRule> HpLoss = new();
        public List<object> Phases = new();
        public bool BypassBlock;
        public Creature? RedirectedReceiver;
        public HashSet<string> UnsupportedReasons = new();
        public bool Unsupported;
        public string Id = Guid.NewGuid().ToString("N");
        public Creature? Dealer;
        public Creature? BlockOwner;
    }

    internal static readonly AsyncLocal<Scope?> scope = new();
    internal static readonly AsyncLocal<Trace?> trace = new();
    internal static readonly AsyncLocal<Trace?> hpTrace = new();
    internal sealed record HpSnapshot(Trace Trace, decimal Input, HpLossRule? Rule, string Method);
    public static TraceCoverage Coverage { get; } = new();
    public static int Matched => Coverage.Matched;
    public static int Unmatched => Coverage.Unmeasured;
    public static bool Enabled { get; internal set; }

    internal static readonly HashSet<object> observed = new(ReferenceEqualityComparer.Instance);
    public static bool Observe(object result, out string eventId)
    {
        object? [] values = new object? []
        {
            result,
            null
        };
        var __runtimeResult = global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.Observe", values);
        eventId = (string)values[1]!;
        return (bool)__runtimeResult!;
    }

    public static bool? Bypasses(DamageResult result) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<bool?>("NativeDamageCapture.Bypasses", result);
    public static void Reset() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.Reset");
    public static void Register()
    {
        var h = new Harmony(Probe.Id + ".DamageTrace");
        try
        {
            var hooks = DamageHookContract.Resolve(typeof(Hook).Assembly);
            var missing = ModifierContracts.MissingStateAccessors(typeof(Hook).Assembly);
            if (missing.Count > 0)
                throw new InvalidOperationException("Missing modifier state: " + string.Join(", ", missing));
            var ctor = typeof(DamageResult).GetConstructor(new[] { typeof(Creature), typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp) }) ?? throw new InvalidOperationException("Unsupported result constructor");
            var lose = AccessTools.Method(typeof(Creature), "LoseHpInternal", new[] { typeof(decimal), typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp) }) ?? throw new InvalidOperationException("Unsupported HP-loss method");
            if (typeof(Creature).Module.ModuleVersionId.ToString() != HpLossAdapterAudit.ModuleId)
                throw new InvalidOperationException("Unaudited result construction module");
            var block = AccessTools.Method(typeof(Creature), "DamageBlockInternal", new[] { typeof(decimal), typeof(MegaCrit.Sts2.Core.ValueProps.ValueProp) }) ?? throw new InvalidOperationException("Unsupported block method");
            if (lose.ReturnType != typeof(DamageResult) || block.ReturnType != typeof(decimal))
                throw new InvalidOperationException("Unsupported damage boundary return types");
            h.Patch(block, prefix: new(typeof(NativeDamageCapture), nameof(BlockPrefix)), postfix: new(typeof(NativeDamageCapture), nameof(BlockPostfix)));
            h.Patch(ctor, postfix: new(typeof(NativeDamageCapture), nameof(ResultConstructed)));
            h.Patch(lose, postfix: new(typeof(NativeDamageCapture), nameof(HpReturned)));
            var damage = typeof(CreatureCmd).GetMethods().Single(m => m.Name == "Damage" && m.GetParameters().Length == 6 && m.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) && m.GetParameters()[2].ParameterType == typeof(decimal));
            h.Patch(damage, prefix: new(typeof(NativeDamageCapture), nameof(DamagePrefix)), postfix: new(typeof(NativeDamageCapture), nameof(DamagePostfix)), finalizer: new(typeof(NativeDamageCapture), nameof(DamageFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyDamage)], prefix: new(typeof(NativeDamageCapture), nameof(ModifyPrefix)), postfix: new(typeof(NativeDamageCapture), nameof(ModifyPostfix)), finalizer: new(typeof(NativeDamageCapture), nameof(ModifyFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyHpLost)], prefix: new(typeof(NativeDamageCapture), nameof(HpPhasePrefix)), postfix: new(typeof(NativeDamageCapture), nameof(HpPhasePostfix)), finalizer: new(typeof(NativeDamageCapture), nameof(HpPhaseFinalizer)));
            h.Patch(hooks[nameof(Hook.ModifyUnblockedDamageTarget)], postfix: new(typeof(NativeDamageCapture), nameof(RedirectPostfix)));
            foreach (var type in typeof(PowerModel).Assembly.GetTypes().Where(t => typeof(AbstractModel).IsAssignableFrom(t)))
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsAbstract && m.ReturnType == typeof(decimal) && m.Name is "ModifyDamageAdditive" or "ModifyDamageMultiplicative" or "ModifyDamageCap" or "EnchantDamageAdditive" or "EnchantDamageMultiplicative"))
                    h.Patch(method, prefix: new(typeof(ModifierCapturePatch), nameof(ModifierCapturePatch.Prefix)), postfix: new(typeof(ModifierCapturePatch), nameof(ModifierCapturePatch.Postfix)), finalizer: new(typeof(ModifierCapturePatch), nameof(ModifierCapturePatch.Finalizer)));
            foreach (var type in typeof(AbstractModel).Assembly.GetTypes().Where(t => typeof(AbstractModel).IsAssignableFrom(t) && t != typeof(AbstractModel)))
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsAbstract && m.Name is "ModifyHpLostBeforeOsty" or "ModifyHpLostBeforeOstyLate" or "ModifyHpLostAfterOsty" or "ModifyHpLostAfterOstyLate"))
                    h.Patch(method, prefix: new(typeof(NativeDamageCapture), nameof(HpModifierPrefix)), postfix: new(typeof(NativeDamageCapture), nameof(HpModifierPostfix)));
            foreach (var type in typeof(AbstractModel).Assembly.GetTypes())
                foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(m => m.Name is "ModifyWeakMultiplier" or "ModifyVulnerableMultiplier"))
                    h.Patch(method, prefix: new(typeof(NativeMultiplierCapture), nameof(NativeMultiplierCapture.ChildPrefix)), postfix: new(typeof(NativeMultiplierCapture), nameof(NativeMultiplierCapture.ChildPostfix)));
            Enabled = true;
            Probe.Emit("damage_trace_registered", new { status = "capture enabled; full coverage audit pending" });
        }
        catch (Exception e)
        {
            h.UnpatchAll(h.Id);
            Enabled = false;
            Probe.Emit("damage_trace_error", new { error = e.ToString() });
        }
    }

    public static void DamagePrefix(out object? __state)
    {
        object? [] values = new object? []
        {
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.DamagePrefix", values);
        __state = (object? )values[0]!;
    }

    public static void DamagePostfix(ref Task<IEnumerable<DamageResult>> __result, object? __state)
    {
        var captured = scope.Value;
        scope.Value = (Scope? )__state;
        if (captured != null)
            __result = Completed(__result, captured);
    }

    internal static async Task<IEnumerable<DamageResult>> Completed(Task<IEnumerable<DamageResult>> task, Scope captured)
    {
        try
        {
            var results = await task;
            try
            {
                var returned = new HashSet<object>(results, ReferenceEqualityComparer.Instance);
                foreach (var orphan in captured.Results.Identities)
                    if (!returned.Contains(orphan))
                        Probe.Emit("damage_binding_error", new { invocationId = captured.Id, reason = "bound result was not returned" });
                foreach (var result in results)
                    if (!captured.Results.Contains(result))
                        Probe.Emit("damage_binding_error", new { invocationId = captured.Id, reason = "returned result was not bound" });
            }
            catch (Exception e)
            {
                Probe.Emit("damage_binding_error", new { error = e.Message });
            }

            return results;
        }
        finally
        {
            captured.Results.Clear();
            captured.Pending.Clear();
            captured.Active = null;
        }
    }

    internal sealed record BlockSnapshot(Trace Trace, Creature Owner, int Before);
    public static void BlockPrefix(Creature __instance, out object? __state)
    {
        object? [] values = new object? []
        {
            __instance,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.BlockPrefix", values);
        __state = (object? )values[1]!;
    }

    public static void BlockPostfix(decimal __result, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.BlockPostfix", __result, __state);
    public static void ResultConstructed(DamageResult __instance) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.ResultConstructed", __instance);
    public static void HpReturned(DamageResult __result) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.HpReturned", __result);
    internal static void Bind(DamageResult result) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.Bind", result);
    public static Exception? DamageFinalizer(Exception? __exception, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("NativeDamageCapture.DamageFinalizer", __exception, __state);
    public static void ModifyPrefix(object[] __args, out object? __state)
    {
        object? [] values = new object? []
        {
            __args,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.ModifyPrefix", values);
        __state = (object? )values[1]!;
    }

    public static void ModifyPostfix(decimal __result, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.ModifyPostfix", __result, __state);
    public static Exception? ModifyFinalizer(Exception? __exception, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("NativeDamageCapture.ModifyFinalizer", __exception, __state);
    public static bool Capturing => trace.Value != null;

    public static void MarkUnsupported(string reason) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.MarkUnsupported", reason);
    public static void RecordMultiplier(AttackMultiplierSnapshot snapshot, decimal observed) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.RecordMultiplier", snapshot, observed);
    public static void RecordAdditive(AdditiveSnapshot snapshot, decimal observed) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.RecordAdditive", snapshot, observed);
    public static void ModifierPostfix(AbstractModel __instance, MethodBase __originalMethod, decimal __result, object? __state, AdditiveSnapshot? additive = null, AttackMultiplierSnapshot? multiplier = null) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.ModifierPostfix", __instance, __originalMethod, __result, __state, additive, multiplier);
    internal static Trace? Pending(Creature target) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Trace?>("NativeDamageCapture.Pending", target);
    public static void HpPhasePrefix(Creature __2, out object? __state)
    {
        object? [] values = new object? []
        {
            __2,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.HpPhasePrefix", values);
        __state = (object? )values[1]!;
    }

    // Harmony's __args writeback can overwrite the original's out-modifiers list.
    // Observe only the required input arguments; never request an argument array here.
    public static void HpPhasePostfix(Creature __2, decimal __3, HpLossHookPhase __7, decimal __result, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.HpPhasePostfix", __2, __3, __7, __result, __state);
    public static Exception? HpPhaseFinalizer(Exception? __exception, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("NativeDamageCapture.HpPhaseFinalizer", __exception, __state);
    public static void HpModifierPrefix(AbstractModel __instance, MethodBase __originalMethod, object[] __args, out object? __state)
    {
        object? [] values = new object? []
        {
            __instance,
            __originalMethod,
            __args,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.HpModifierPrefix", values);
        __state = (object? )values[3]!;
    }

    public static void HpModifierPostfix(decimal __result, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.HpModifierPostfix", __result, __state);
    public static void RedirectPostfix(object[] __args, Creature __result) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("NativeDamageCapture.RedirectPostfix", __args, __result);
    public static AmplificationResult? Resolve(Creature target, DamageResult result, string eventId) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<AmplificationResult?>("NativeDamageCapture.Resolve", target, result, eventId);
}
