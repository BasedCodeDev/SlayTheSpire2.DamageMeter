using System.Reflection;
using System.Runtime.CompilerServices;
using Based.Spires2DamageMeter;
using HarmonyLib;

public static class RegistrationChecks
{
    private static readonly SourceContext<string> context = new();
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string?> observed = new();
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task Plain(string owner, Task wait) { observed.Enqueue(context.Current); await wait; observed.Enqueue(context.Current); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static async Task<string?> Generic(string owner, Task wait, string? result) { observed.Enqueue(context.Current); await wait; observed.Enqueue(context.Current); return result; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task SynchronousFailure(string owner) => throw new InvalidOperationException("original");
    public static void Prefix(string owner, out string? __state) { __state = context.Current; context.Current = owner; }
    public static void TaskPostfix(ref Task __result, string? __state) { var current = context.Current; context.Current = __state; __result = context.Complete(__result, current, __state); }
    public static void GenericPostfix(ref Task<string?> __result, string? __state) { var current = context.Current; context.Current = __state; __result = context.Complete(__result, current, __state); }
    public static Exception? Finalizer(Exception? __exception, string? __state) { context.Current = __state; return __exception; }
    public static void InvalidPostfix(int nonexistentArgument) { }
    public static void IndependentPrefix() { }
    public static void Unsupported() { }

    public static void Run(Action<bool, string> check) => RunAsync(check).GetAwaiter().GetResult();
    private static async Task RunAsync(Action<bool, string> check)
    {
        var owner = new Harmony("Based.Tests.Ownership"); var independent = new Harmony("Based.Tests.Independent");
        MethodInfo M(string n) => typeof(RegistrationChecks).GetMethod(n)!;
        HarmonyMethod H(string n) => new(M(n));
        PatchSpec Spec(string n, bool generic = false) => new(M(n), H(nameof(Prefix)), H(generic ? nameof(GenericPostfix) : nameof(TaskPostfix)), H(nameof(Finalizer)));
        try
        {
            OwnershipHooks.Install(owner, new[] { Spec(nameof(Plain)), Spec(nameof(Generic), true), Spec(nameof(SynchronousFailure)) });
            context.Current = "parent";
            var gate = new TaskCompletionSource();
            var task = Plain("A", gate.Task); var result = Generic("B", gate.Task, "value");
            check(context.Current == "parent", "patched task wrappers restore caller immediately"); gate.SetResult(); await Task.WhenAll(task, result);
            check(await result == "value" && observed.Count(s => s == "A") == 2 && observed.Count(s => s == "B") == 2, "Task and Task<T> interleaved source isolation");
            check(await Generic("C", Task.CompletedTask, null) == null, "generic null result preserved");
            try { _ = SynchronousFailure("bad"); check(false, "must throw"); } catch (InvalidOperationException e) { check(e.Message == "original" && context.Current == "parent", "synchronous exception and parent context preserved"); }
            check(StatusOwnershipLedger.Allocate(Array.Empty<Share>(), 8).Single() == new Share(StatusOwnershipLedger.Unknown, "Missing stack history", 8), "disabled ownership retains HP as unknown");
            var error = new InvalidOperationException("fault");
            try { await Plain("fault", Task.FromException(error)); } catch (InvalidOperationException e) { check(ReferenceEquals(e, error) && context.Current == "parent", "fault identity preserved"); }
            var cts = new CancellationTokenSource(); cts.Cancel();
            var cancelled = Generic("cancel", Task.FromCanceled(cts.Token), "unused");
            try { await cancelled; } catch (OperationCanceledException) { check(cancelled.IsCanceled && context.Current == "parent", "cancellation preserved"); }
            context.Current = "outer"; await Plain("inner", Task.CompletedTask); check(context.Current == "outer", "nested source restoration");
            owner.UnpatchAll(owner.Id);
            independent.Patch(M(nameof(Plain)), prefix: H(nameof(IndependentPrefix)));
            try { OwnershipHooks.Install(owner, new[] { Spec(nameof(Plain)), new PatchSpec(M(nameof(Generic)), Postfix: H(nameof(InvalidPostfix))) }); check(false, "injected registration failure required"); }
            catch { check(!Harmony.GetPatchInfo(M(nameof(Plain)))!.Owners.Contains(owner.Id) && Harmony.GetPatchInfo(M(nameof(Plain)))!.Owners.Contains(independent.Id), "atomic rollback preserves independent hooks"); }
            try { OwnershipHooks.Resolve(typeof(RegistrationChecks), nameof(Unsupported)); check(false, "unsupported return must fail"); } catch (InvalidOperationException e) { check(e.Message.Contains("Task"), "unsupported shape diagnostic"); }
            try { OwnershipHooks.Resolve(typeof(RegistrationChecks), "Missing"); check(false, "missing target must fail"); } catch (MissingMethodException) { check(true, "missing target diagnostic"); }
        }
        finally { owner.UnpatchAll(owner.Id); independent.UnpatchAll(independent.Id); }
    }
}
