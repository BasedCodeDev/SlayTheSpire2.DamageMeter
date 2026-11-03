using System.Reflection;
using System.Runtime.CompilerServices;
using Based.Spires2DamageMeter;
using Based.Spires2DamageMeter.Hosting;
using Based.Spires2DamageMeter.Reload;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Combat;

public static class NativeObserverChecks
{
    private static readonly IEnumerable<AbstractModel> nativeModifiers = new List<AbstractModel>();
    private static readonly object output = new();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static decimal HpBoundary(IRunState run, ICombatState combat, Creature target, decimal amount, ValueProp props,
        Creature dealer, CardModel card, HpLossHookPhase phase, out IEnumerable<AbstractModel> modifiers)
    { modifiers = nativeModifiers; return amount; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static decimal RefBoundary(ref decimal amount, out object nativeOutput)
    { amount += 1; nativeOutput = output; return amount; }
    private sealed record Slot(object Value, PropertyInfo Property)
    {
        public object? Get() => Property.GetValue(Value);
        public void Set(object? value) => Property.SetValue(Value, value);
    }
    private static Slot Ambient(Type owner, string field)
    {
        var slot = owner.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        return new(slot, slot.GetType().GetProperty("Value")!);
    }
    private static object State(Type owner, string name) => Activator.CreateInstance(owner.GetNestedType(name, BindingFlags.NonPublic)!, true)!;

    public static void Run(Action<bool, string> check)
    {
        var handler = RuntimeCalls.Handler!;
        var health = Probe.Health;
        var scope = Ambient(typeof(NativeDamageCapture), "scope");
        var trace = Ambient(typeof(NativeDamageCapture), "trace");
        var hp = Ambient(typeof(NativeDamageCapture), "hpTrace");
        var multiplier = Ambient(typeof(NativeMultiplierCapture), "current");
        var saved = new[] { scope.Get(), trace.Get(), hp.Get(), multiplier.Get() };
        var previousScope = State(typeof(NativeDamageCapture), "Scope");
        var previousTrace = State(typeof(NativeDamageCapture), "Trace");
        var previousFrame = State(typeof(NativeMultiplierCapture), "Frame");
        var childFrame = State(typeof(NativeMultiplierCapture), "Frame");
        childFrame.GetType().GetField("Parent")!.SetValue(childFrame, previousFrame);
        var creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
        typeof(Creature).GetProperty("Block")!.SetValue(creature, 10);
        var method = typeof(NativeObserverChecks).GetMethod(nameof(Run))!;
        var failure = new InvalidOperationException("original native failure");
        var harmony = new Harmony("Based.DamageMeter.NativeObserverChecks");
        try
        {
            var block = typeof(Creature).GetMethod("DamageBlockInternal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            harmony.Patch(block, prefix: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.BlockPrefix)), postfix: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.BlockPostfix)));
            foreach (var operation in new[] { "NativeDamageCapture.BlockPrefix", "NativeDamageCapture.BlockPostfix" })
            {
                RuntimeCalls.Handler = (key, args) => key == operation ? throw new MissingMethodException(operation) : handler(key, args);
                typeof(Creature).GetProperty("Block")!.SetValue(creature, 10);
                var result = block.Invoke(creature, [6m, Enum.ToObject(block.GetParameters()[1].ParameterType, 0)]);
                check((decimal)result! == 6 && creature.Block == 4, "real native block damage survives observer failure in " + operation);
                check(Probe.Health.Contains(operation) && Probe.Health.Contains(RuntimeHost.BuildId), "native callback failure identifies operation and runtime");
            }
            var cases = new (string Operation, Action Call)[]
            {
                ("NativeDamageCapture.DamagePrefix", () => { NativeDamageCapture.DamagePrefix(out var state); check(ReferenceEquals(state, previousScope) && ReferenceEquals(scope.Get(), previousScope), "failed damage prefix restores parent scope"); }),
                ("NativeDamageCapture.ModifyPrefix", () => { NativeDamageCapture.ModifyPrefix([], out var state); check(ReferenceEquals(state, previousTrace) && ReferenceEquals(trace.Get(), previousTrace), "failed modifier prefix restores parent trace"); }),
                ("NativeDamageCapture.HpPhasePrefix", () => { NativeDamageCapture.HpPhasePrefix(creature, out var state); check(ReferenceEquals(state, previousTrace) && ReferenceEquals(hp.Get(), previousTrace), "failed HP prefix restores parent trace"); }),
                ("NativeDamageCapture.HpModifierPrefix", () => { NativeDamageCapture.HpModifierPrefix(null!, method, [], out var state); check(state == null, "failed HP modifier prefix leaves inert state"); }),
                ("ModifierCapturePatch.Prefix", () => { ModifierCapturePatch.Prefix(null!, method, [], out var state); check(state == null && ReferenceEquals(multiplier.Get(), previousFrame), "failed modifier capture restores parent frame"); }),
                ("NativeMultiplierCapture.Prefix", () => { NativeMultiplierCapture.Prefix(null!, method, [], out var state); check(state == null && ReferenceEquals(multiplier.Get(), previousFrame), "failed multiplier prefix restores parent frame"); }),
                ("NativeMultiplierCapture.ChildPrefix", () => NativeMultiplierCapture.ChildPrefix(null!, method, [], out _)),
                ("NativeDamageCapture.ResultConstructed", () => NativeDamageCapture.ResultConstructed(null!)),
                ("NativeDamageCapture.HpReturned", () => NativeDamageCapture.HpReturned(null!)),
                ("NativeDamageCapture.ModifyPostfix", () => { NativeDamageCapture.ModifyPostfix(6, previousTrace); check(ReferenceEquals(trace.Get(), previousTrace), "failed modifier postfix restores parent trace"); }),
                ("NativeDamageCapture.HpPhasePostfix", () => { NativeDamageCapture.HpPhasePostfix(creature, 6, default, 6, previousTrace); check(ReferenceEquals(hp.Get(), previousTrace), "failed HP postfix restores parent trace"); }),
                ("NativeDamageCapture.HpModifierPostfix", () => NativeDamageCapture.HpModifierPostfix(6, null)),
                ("NativeDamageCapture.RedirectPostfix", () => NativeDamageCapture.RedirectPostfix([], creature)),
                ("ModifierCapturePatch.Postfix", () => ModifierCapturePatch.Postfix(null!, method, 6, null)),
                ("NativeMultiplierCapture.Postfix", () => NativeMultiplierCapture.Postfix(null!, method, 6, null)),
                ("NativeMultiplierCapture.ChildPostfix", () => NativeMultiplierCapture.ChildPostfix(6, null)),
                ("EffectiveCapture.Before", () => { EffectiveCapture.Before(creature, out var state); check(state == creature.Block, "failed effective prefix retains native block snapshot"); }),
                ("EffectiveCapture.Gained", () => EffectiveCapture.Gained(creature, 0)),
                ("EffectiveCapture.AfterClearCompleted", () => { Task task = Task.CompletedTask; EffectiveCapture.Cleared(creature, 10, ref task); task.GetAwaiter().GetResult(); })
            };
            foreach (var test in cases)
            {
                scope.Set(previousScope); trace.Set(previousTrace); hp.Set(previousTrace); multiplier.Set(previousFrame);
                RuntimeCalls.Handler = (key, args) =>
                {
                    if (key != test.Operation) return handler(key, args);
                    // Model a partially executed observer, not just an absent dispatcher.
                    scope.Set(null); trace.Set(null); hp.Set(null); multiplier.Set(null);
                    throw new MissingMethodException(key);
                };
                test.Call();
                check(Probe.Health.Contains(test.Operation), "lower-level native observer failure contained: " + test.Operation);
            }
            var finalizers = new (string Operation, Func<Exception?, Exception?> Call, Slot Slot, object Parent)[]
            {
                ("NativeDamageCapture.DamageFinalizer", e => NativeDamageCapture.DamageFinalizer(e, previousScope), scope, previousScope),
                ("NativeDamageCapture.ModifyFinalizer", e => NativeDamageCapture.ModifyFinalizer(e, previousTrace), trace, previousTrace),
                ("NativeDamageCapture.HpPhaseFinalizer", e => NativeDamageCapture.HpPhaseFinalizer(e, previousTrace), hp, previousTrace),
                ("NativeMultiplierCapture.Finalizer", e => NativeMultiplierCapture.Finalizer(e, childFrame), multiplier, previousFrame),
                ("ModifierCapturePatch.Finalizer", e => ModifierCapturePatch.Finalizer(e, WithChain(childFrame)), multiplier, previousFrame)
            };
            foreach (var finalizer in finalizers)
            {
                RuntimeCalls.Handler = (key, args) => key == finalizer.Operation ? throw new MissingMethodException(key) : handler(key, args);
                check(ReferenceEquals(finalizer.Call(failure), failure) && finalizer.Call(null) == null && ReferenceEquals(finalizer.Slot.Get(), finalizer.Parent),
                    "failed finalizer retains exact native exception/success and parent state: " + finalizer.Operation);
                RuntimeCalls.Handler = (key, args) => key == finalizer.Operation ? new Exception("observer attempted to replace native exception") : handler(key, args);
                check(ReferenceEquals(finalizer.Call(failure), failure) && finalizer.Call(null) == null, "observer cannot replace native finalizer outcome: " + finalizer.Operation);
            }
            RuntimeCalls.Handler = handler;
            scope.Set(null); trace.Set(null); hp.Set(null); multiplier.Set(null);
            CheckArguments(handler, harmony, failure, check);
            CheckTasks(creature, failure, check);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            RuntimeCalls.Handler = handler; Probe.Health = health;
            scope.Set(saved[0]); trace.Set(saved[1]); hp.Set(saved[2]); multiplier.Set(saved[3]);
        }
    }

    private static object WithChain(object frame)
    {
        var state = State(typeof(ModifierCapturePatch), "State");
        state.GetType().GetField("Chain")!.SetValue(state, frame);
        return state;
    }

    private static void CheckArguments(Func<string, object?[], object?> handler, Harmony harmony, Exception failure, Action<bool, string> check)
    {
        harmony.Patch(typeof(NativeObserverChecks).GetMethod(nameof(HpBoundary))!,
            prefix: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.HpPhasePrefix)),
            postfix: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.HpPhasePostfix)),
            finalizer: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.HpPhaseFinalizer)));
        harmony.Patch(typeof(NativeObserverChecks).GetMethod(nameof(RefBoundary))!,
            postfix: new(typeof(NativeDamageCapture), nameof(NativeDamageCapture.BlockPostfix)));
        foreach (var operation in new[] { "NativeDamageCapture.HpPhasePrefix", "NativeDamageCapture.HpPhasePostfix", "NativeDamageCapture.HpPhaseFinalizer" })
        {
            RuntimeCalls.Handler = (key, args) => key == operation ? throw new MissingMethodException(key) : handler(key, args);
            var amount = HpBoundary(null!, null!, null!, 6, default, null!, null!, default, out var modifiers);
            check(amount == 6 && ReferenceEquals(modifiers, nativeModifiers), "failed HP observer preserves exact native result/out-list identity: " + operation);
        }
        RuntimeCalls.Handler = (key, args) => key == "NativeDamageCapture.BlockPostfix" ? throw failure : handler(key, args);
        decimal input = 5;
        var result = RefBoundary(ref input, out var nativeOutput);
        check(result == 6 && input == 6 && ReferenceEquals(nativeOutput, output), "failed block observer preserves native result, ref writeback and out identity");
        RuntimeCalls.Handler = handler;
    }

    private static void CheckTasks(Creature creature, Exception failure, Action<bool, string> check)
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        foreach (var outcome in new[] { "result", "fault", "cancellation" })
        {
            NativeDamageCapture.DamagePrefix(out var state);
            var pending = new TaskCompletionSource<IEnumerable<DamageResult>>();
            Task<IEnumerable<DamageResult>> damage = pending.Task;
            NativeDamageCapture.DamagePostfix(ref damage, state);
            var clearPending = new TaskCompletionSource(); Task clear = clearPending.Task;
            EffectiveCapture.Cleared(creature, 0, ref clear);
            check(!damage.IsCompleted && !clear.IsCompleted, "native damage/block-clear wrappers remain pending: " + outcome);
            var results = Array.Empty<DamageResult>();
            if (outcome == "result") { pending.SetResult(results); clearPending.SetResult(); check(ReferenceEquals(damage.GetAwaiter().GetResult(), results), "damage wrapper returns exact native result collection"); clear.GetAwaiter().GetResult(); }
            else if (outcome == "fault")
            {
                pending.SetException(failure); clearPending.SetException(failure);
                foreach (var task in new Task[] { damage, clear })
                    try { task.GetAwaiter().GetResult(); check(false, "native task fault missing"); }
                    catch (Exception e) { check(ReferenceEquals(e, failure), "damage/block-clear wrapper preserves original fault identity"); }
            }
            else
            {
                pending.SetCanceled(cancelled.Token); clearPending.SetCanceled(cancelled.Token);
                foreach (var task in new Task[] { damage, clear })
                    try { task.GetAwaiter().GetResult(); check(false, "native cancellation missing"); }
                    catch (OperationCanceledException e) { check(task.IsCanceled && e.CancellationToken == cancelled.Token, "damage/block-clear wrapper preserves native cancellation token/status"); }
            }
        }
    }
}
