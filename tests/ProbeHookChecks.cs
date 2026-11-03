using System.Reflection;
using System.Runtime.CompilerServices;
using Based.Spires2DamageMeter;
using Based.Spires2DamageMeter.Hosting;
using Based.Spires2DamageMeter.Reload;

public static class ProbeHookChecks
{
    private static readonly FieldInfo active = typeof(Probe).GetField("active", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static MethodInfo PowerHook => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "sts2")
        .GetType("MegaCrit.Sts2.Core.Hooks.Hook", true)!.GetMethod("AfterPowerAmountChanged")!;

    public static void Run(Action<bool, string> check)
    {
        var previous = active.GetValue(null);
        try
        {
            active.SetValue(null, false);
            var method = PowerHook;
            var args = new object[method.GetParameters().Length];
            Probe.HookPrefix(method, args);
            var pending = new TaskCompletionSource();
            Task observed = pending.Task;
            Probe.HookPostfix(method, args, ref observed);
            check(ReferenceEquals(observed, pending.Task) && !observed.IsCompleted, "real power hook dispatch preserves pending native task");
            pending.SetResult(); observed.GetAwaiter().GetResult();
            var failure = new InvalidOperationException("native power fixture failure");
            observed = Task.FromException(failure); var faulted = observed;
            Probe.HookPostfix(method, args, ref observed);
            try { observed.GetAwaiter().GetResult(); throw new Exception("Native failure swallowed"); }
            catch (InvalidOperationException e) { check(ReferenceEquals(observed, faulted) && ReferenceEquals(e, failure), "real power hook retains native task and exception identity"); }
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            observed = Task.FromCanceled(cancellation.Token); var cancelled = observed;
            Probe.HookPostfix(method, args, ref observed);
            try { observed.GetAwaiter().GetResult(); throw new Exception("Native cancellation swallowed"); }
            catch (OperationCanceledException e) { check(ReferenceEquals(observed, cancelled) && e.CancellationToken == cancellation.Token, "real power hook retains native cancellation"); }
        }
        finally { active.SetValue(null, previous); }
    }

    // Requires initialized Godot for error logging; used only by the isolated native gate.
    public static void RunNative(Action<bool, string> check)
    {
        Run(check);
        var previous = active.GetValue(null);
        var health = Probe.Health;
        var handler = RuntimeCalls.Handler!;
        try
        {
            active.SetValue(null, true);
            var method = PowerHook;
            var args = new object[method.GetParameters().Length];
            var powerIndex = Array.FindIndex(method.GetParameters(), p => p.Name == "power");
            var thorns = method.DeclaringType!.Assembly.GetType("MegaCrit.Sts2.Core.Models.Powers.ThornsPower", true)!;
            args[powerIndex] = RuntimeHelpers.GetUninitializedObject(thorns);
            var task = Task.CompletedTask;
            Probe.HookPostfix(method, args, ref task);
            check(ReferenceEquals(task, Task.CompletedTask) && Probe.Health == health, "combat setup Thorns observation reaches real runtime without disrupting native task");
            foreach (var operation in new[] { "Probe.HookPrefix", "Probe.HandleHook" })
            {
                RuntimeCalls.Handler = (key, values) => key == operation ? throw new MissingMethodException(key) : handler(key, values);
                var pending = new TaskCompletionSource();
                task = pending.Task;
                if (operation == "Probe.HookPrefix") Probe.HookPrefix(method, args);
                else Probe.HookPostfix(method, args, ref task);
                check(ReferenceEquals(task, pending.Task) && !task.IsCompleted, "observer failure contains " + operation + " without replacing native task");
                check(Probe.Health.StartsWith("INCOMPLETE:") && Probe.Health.Contains(operation) && Probe.Health.Contains(RuntimeHost.BuildId), "observer failure reports operation and active runtime build");
                var failure = new InvalidOperationException("native failure after observer rejection");
                pending.SetException(failure);
                try { task.GetAwaiter().GetResult(); throw new Exception("Native task failure swallowed"); }
                catch (InvalidOperationException e) { check(ReferenceEquals(e, failure), "contained observer failure preserves subsequent native exception identity"); }
                RuntimeCalls.Handler = handler;
            }
        }
        finally { RuntimeCalls.Handler = handler; active.SetValue(null, previous); Probe.Health = health; }
    }
}
