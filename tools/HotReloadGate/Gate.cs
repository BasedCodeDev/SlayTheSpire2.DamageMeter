using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Based.Spires2DamageMeter.Hosting;
using Based.Spires2DamageMeter.Reload;
using Godot;
using HarmonyLib;
using Based.Spires2DamageMeter;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Saves;

public partial class Gate : Node
{
    private static RuntimeGeneration? current;
    private readonly List<WeakReference> retired = new();
    private readonly Harmony patches = new("Based.DamageMeter.ReloadGate");
    private int cycles;
    private long counter;
    private string payload = "";
    private string report = "";
    private readonly List<string> evidence = new();
    private bool failed, actualStarted, rejectionsChecked, watcherStarted, watcherChecked;
    private readonly System.Diagnostics.Stopwatch watcherTime = new();
    private static bool actualRouting;
    private int actualCycles;
    private string install = "", baseline = "", alternate = "";
    private string gameData = "", godotPlugins = "";
    private readonly object identity = new();
    private TaskCompletionSource<IEnumerable<DamageResult>>? pendingDamage;
    private Task<IEnumerable<DamageResult>>? observedDamage;
    private TaskCompletionSource? pendingSource, pendingSave;
    private Task? observedSource, observedSave;
    private readonly InvalidOperationException nativeFailure = new("native save fixture failure");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Target(int number) => number;
    public static void Postfix(ref int __result)
        => __result = actualRouting ? (int)EngineDamage.Normalize(__result, 0, 0).Hp : (int)current!.Invoke("calculate", __result)!;

    public override void _Ready()
    {
        try
        {
            var args = OS.GetCmdlineUserArgs();
            AssemblyLoadContext.Default.Resolving += (_, name) =>
            {
                var dependency = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
                return File.Exists(dependency) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(dependency) : null;
            };
            payload = args[0]; report = args[1];
            if (args.Length >= 5) { install = args[2]; baseline = args[3]; alternate = args[4]; }
            if (args.Length >= 7) { gameData = args[5]; godotPlugins = args[6]; }
            patches.Patch(typeof(Gate).GetMethod(nameof(Target))!, postfix: new HarmonyMethod(typeof(Gate), nameof(Postfix)));
            evidence.Add("native=" + Engine.GetVersionInfo()["string"]);
        }
        catch (Exception e) { Fail(e); }
    }

    public override void _Process(double delta)
    {
        if (failed) return;
        try
        {
            if (cycles >= 50)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                if (retired.Any(r => r.IsAlive)) throw new Exception("Retired payload context remains rooted");
                if (!evidence.Any(s => s.StartsWith("PASS 50 native"))) evidence.Add("PASS 50 native load/UI/calculate/dispose/unload cycles; state=" + counter);
                if (install.Length > 0 && actualCycles < 50) { RunActual(); return; }
                if (actualStarted && !rejectionsChecked) { CheckRejections(); return; }
                if (actualStarted && !watcherChecked) { CheckFilesystemWatcher(); return; }
                if (gameData.Length > 0)
                {
                    var payloads = new[] { baseline, alternate }.Select(file => BuildManifest.Read(file).Validate(install)).ToArray();
                    GameLoadingChecks.Run(Path.Combine(install, "Based.Spires2DamageMeter.dll"), gameData, godotPlugins, payloads,
                        (ok, name) => { if (!ok) throw new Exception(name); evidence.Add("PASS " + name); });
                    gameData = "";
                }
                RuntimeHost.CollectRetired();
                if (RuntimeHost.RetainedGenerationCount != 0) throw new Exception("Real runtime contexts remain rooted");
                patches.UnpatchAll(patches.Id);
                if (actualStarted) evidence.Add("PASS 50 real-runtime reloads: changed UI/calculation, preserved accounting/deduplication/pending block/layout, pending damage/source/save tasks, collected generations");
                File.WriteAllLines(report, evidence); GD.Print(evidence.Last()); GetTree().Quit(); return;
            }
            RunCycle();
        }
        catch (Exception e) { Fail(e); }
    }

    private void CheckFilesystemWatcher()
    {
        var pointer = Path.Combine(install, "runtime/current.json");
        if (!watcherStarted)
        {
            RuntimeHost.Pump(0);
            File.Copy(alternate, pointer + ".tmp", true);
            File.Replace(pointer + ".tmp", pointer, pointer + ".previous");
            watcherTime.Start(); watcherStarted = true;
        }
        RuntimeHost.Pump(0);
        if (RuntimeHost.BuildId == BuildManifest.Read(alternate).BuildId)
        {
            if (watcherTime.ElapsedMilliseconds >= 950) throw new Exception("Runtime was not activated before the polling fallback");
            evidence.Add("PASS filesystem notification activates atomic deployment before the one-second fallback poll");
            watcherChecked = true;
        }
        else if (watcherTime.ElapsedMilliseconds >= 950) throw new Exception("Filesystem notification did not activate deployment: " + RuntimeHost.Status);
    }

    private void CheckRejections()
    {
        var pointer = Path.Combine(install, "runtime/current.json");
        var retained = RuntimeHost.BuildId;
        var ui = MeterPanel.instance;
        foreach (var file in Directory.GetFiles(Path.Combine(Path.GetDirectoryName(report)!, "rejected"), "*.json"))
        {
            File.Copy(file, pointer + ".tmp", true); File.Move(pointer + ".tmp", pointer, true);
            RuntimeHost.Pump(1);
            if (RuntimeHost.BuildId != retained || !ReferenceEquals(ui, MeterPanel.instance) || !RuntimeHost.Status.StartsWith("Reload rejected"))
                throw new Exception("Rejected build replaced the active runtime: " + file + ": " + RuntimeHost.Status);
        }
        File.Copy(baseline, pointer + ".tmp", true); File.Move(pointer + ".tmp", pointer, true);
        RuntimeHost.Pump(1);
        if (!ReferenceEquals(ui, MeterPanel.instance)) throw new Exception("Duplicate publication unnecessarily rebuilt UI");
        File.Copy(alternate, pointer + ".tmp", true); File.Move(pointer + ".tmp", pointer, true);
        File.Copy(baseline, pointer + ".tmp", true); File.Move(pointer + ".tmp", pointer, true);
        RuntimeHost.Pump(1);
        if (RuntimeHost.BuildId != retained || !ReferenceEquals(ui, MeterPanel.instance)) throw new Exception("Rapid publications did not converge on newest build");
        evidence.Add("PASS rejected broken DLL, bad hash, incompatible host/state/API, malformed manifest, failing/null/parented UI; previous runtime/UI retained; duplicate/rapid publications coalesced");
        rejectionsChecked = true;
    }

    private void RunActual()
    {
        if (!actualStarted)
        {
            RuntimeHost.StartNativeChecks(install);
            ProbeHookChecks.RunNative((ok, name) => { if (!ok) throw new Exception(name); evidence.Add("PASS " + name); });
            NativeObserverChecks.Run((ok, name) => { if (!ok) throw new Exception(name); evidence.Add("PASS " + name); });
            actualRouting = actualStarted = true;
            RunSession.Active = true; RunSession.Encounter = "gate-combat";
            Probe.Names["A"] = "Gate player";
            Probe.Meter.AddResolved(new(identity, "Direct", 9, 3, new[] { new Share("A", "Strike", 9) }));
            var credits = Probe.Credited.Add("gate-first", "Direct", 9, 3, new[] { new Share("A", "Strike", 9) }, null, null);
            Probe.Effective.Add("gate-first", "enemy", credits, false);
            NativeDamageCapture.DamagePrefix(out var damageState);
            pendingDamage = new(); observedDamage = pendingDamage.Task;
            NativeDamageCapture.DamagePostfix(ref observedDamage, damageState);
            Probe.SourcePrefix(typeof(Gate).GetMethod(nameof(Target))!, new object[] { 4 }, out var sourceState);
            Probe.changes.Begin(Probe.origin.Current!.OperationId, "pending", 0, "A", "Power");
            pendingSource = new(); observedSource = pendingSource.Task;
            Probe.SourceTaskPostfix(ref observedSource, sourceState);
            var save = (SerializableRun)RuntimeHelpers.GetUninitializedObject(typeof(SerializableRun));
            RunSession.SavePrefix(save, out var saveState);
            pendingSave = new(); observedSave = pendingSave.Task;
            RunSession.SavePostfix(ref observedSave, saveState);
            MeterPanel.Ensure();
            return;
        }
        if (!MeterPanel.instance!.IsInsideTree()) return;
        if (actualCycles == 0)
        {
            var firstUi = MeterPanel.instance;
            firstUi.panel.Position = new Vector2(73, 95);
            firstUi.panel.Size = new Vector2(389, 0);
            firstUi.metric.Select(firstUi.metric.GetItemIndex(1));
            firstUi.scopeIndex = 1; firstUi.UpdateScopeToggle();
            firstUi.showUnattributed = true; firstUi.unattributedToggle.ButtonPressed = true; firstUi.UpdateUnattributedToggle();
            firstUi.ApplyCollapsedState(true);
            using var key = new InputEventKey { Keycode = Key.F8, Pressed = true };
            firstUi._Input(key);
        }
        var beforeLayout = MeterPanel.instance.CapturePresentation();
        var tooltip = (MeterTooltipContent)MeterPanel.instance.resize._MakeCustomTooltip("fixture");
        var tooltipWindow = new Window(); GetTree().Root.AddChild(tooltipWindow); tooltipWindow.AddChild(tooltip); tooltipWindow.Show();
        if (!tooltipWindow.Visible) throw new Exception("Fixture tooltip window did not open");
        var isAlternate = actualCycles % 2 == 0;
        var source = isAlternate ? alternate : baseline;
        var expected = BuildManifest.Read(source);
        var pointer = Path.Combine(install, "runtime/current.json");
        File.Copy(source, pointer + ".tmp", true);
        File.Move(pointer + ".tmp", pointer, true);
        RuntimeHost.Pump(1);
        if (RuntimeHost.BuildId != expected.BuildId) throw new Exception("Actual runtime activation failed: " + RuntimeHost.Status);
        var meter = MeterPanel.instance!;
        void NoticeFrame(double delta) { meter.elapsed = -100; meter._Process(delta); }
        NoticeFrame(0);
        var notice = meter.GetNode<Button>("ReloadNotice");
        if (!notice.TooltipText.Contains(expected.BuildId)) throw new Exception("Reload notice does not identify activated build");
        var wasVisible = meter.panel.Visible;
        meter.panel.Visible = false;
        NoticeFrame(1);
        if (notice.Visible || notice.GetMeta("age").AsDouble() != 0 || !notice.GetMeta("pending").AsBool()) throw new Exception("Hidden meter consumed reload notice");
        meter.panel.Visible = true;
        meter.panel.Position = beforeLayout.Position;
        NoticeFrame(.125);
        if (!notice.Visible || notice.Scale.X <= 1 || notice.Position.X >= beforeLayout.Position.X) throw new Exception("Reload notice is not pulsing left of meter");
        NoticeFrame(60);
        if (!notice.Visible || notice.Modulate.A != 1 || notice.Scale != Vector2.One) throw new Exception("Reload notice did not remain visible after animation");
        notice.EmitSignal(BaseButton.SignalName.Pressed);
        NoticeFrame(0);
        if (notice.Visible || notice.GetMeta("pending").AsBool()) throw new Exception("Click did not dismiss reload notice");
        NoticeFrame(60);
        if (notice.Visible) throw new Exception("Dismissed reload notice reappeared without another reload");
        meter.panel.Visible = wasVisible;
        meter.panel.Position = beforeLayout.Position; // _Process also clamps to the fixture viewport.
        ProbeHookChecks.Run((ok, name) => { if (!ok) throw new Exception(name); });
        if (actualCycles == 0)
        {
            ProbeHookChecks.RunNative((ok, name) => { if (!ok) throw new Exception(name); evidence.Add("PASS after replacement: " + name); });
            NativeObserverChecks.Run((ok, name) => { if (!ok) throw new Exception(name); evidence.Add("PASS after replacement: " + name); });
        }
        RuntimeCalls.Call("RunSession.Error", new InvalidOperationException("fixture anonymous log serialization"));
        if (Target(4) != (isAlternate ? 5 : 4)) throw new Exception("Changed runtime calculation was not activated");
        if (MeterPanel.instance.grab.Icon.ResourceName != (isAlternate ? "expand" : "drag")) throw new Exception("Changed runtime UI was not activated");
        if (meter.collapse.Icon.ResourceName != "expand" || meter.scopeToggle.Icon.ResourceName != "run" || meter.unattributedToggle.Icon.ResourceName != "shown") throw new Exception("Restored toggle icons disagree with state");
        meter.scopeIndex = 0; meter.UpdateScopeToggle();
        meter.showUnattributed = false; meter.UpdateUnattributedToggle();
        meter.ApplyCollapsedState(false);
        if (meter.collapse.Icon.ResourceName != "collapse" || meter.scopeToggle.Icon.ResourceName != "fight" || meter.unattributedToggle.Icon.ResourceName != "hidden") throw new Exception("Changed toggle state did not change icons");
        if (notice.Icon.ResourceName != "reload" || meter.metric.GetThemeIcon("arrow").ResourceName != "dropdown") throw new Exception("Notification or dropdown icon missing");
        meter.metric.GetPopup().EmitSignal(Window.SignalName.AboutToPopup);
        if (meter.metric.GetThemeIcon("arrow").ResourceName != "dropdown-open") throw new Exception("Open dropdown icon missing");
        meter.metric.GetPopup().EmitSignal(Popup.SignalName.PopupHide);
        if (meter.metric.GetThemeIcon("arrow").ResourceName != "dropdown") throw new Exception("Closed dropdown icon missing");
        foreach (var icon in new[] { meter.grab.Icon, meter.collapse.Icon, meter.scopeToggle.Icon, meter.unattributedToggle.Icon, notice.Icon })
            if (icon.GetSize() != new Vector2(20,20)) throw new Exception("Icon did not retain compact display size");
        meter.scopeIndex = beforeLayout.Scope; meter.UpdateScopeToggle();
        meter.showUnattributed = beforeLayout.ShowUnattributed; meter.UpdateUnattributedToggle();
        meter.ApplyCollapsedState(beforeLayout.Collapsed);
        meter.expandedWidth = beforeLayout.Width;
        if (MeterPanel.instance.CapturePresentation() != beforeLayout) throw new Exception($"Layout, selected metric/scope, collapsed or F8 state changed during reload: {beforeLayout} -> {MeterPanel.instance.CapturePresentation()}");
        if (tooltipWindow.Visible) throw new Exception("Retired tooltip window was not closed");
        if (Probe.Meter.Rows["A"].Hp != 9 || Probe.Meter.Rows["A"].Block != 3 || Probe.Effective.PendingFor("A") != 3 || Probe.Credited.Credits.Sum(c => c.Hp) != 9)
            throw new Exception("Compatible accounting state changed during reload");
        if (Probe.Meter.AddResolved(new(identity, "Direct", 9, 3, new[] { new Share("A", "Strike", 9) }))) throw new Exception("Damage identity was lost during reload");
        if (actualCycles == 0)
        {
            pendingDamage!.SetResult(Array.Empty<DamageResult>()); observedDamage!.GetAwaiter().GetResult();
            pendingSource!.SetResult(); observedSource!.GetAwaiter().GetResult();
            if (Probe.changes.Unresolved != 1) throw new Exception("Pending source cleanup did not reach the new runtime");
            pendingSave!.SetException(nativeFailure);
            try { observedSave!.GetAwaiter().GetResult(); throw new Exception("Native save failure was swallowed"); }
            catch (InvalidOperationException e) when (ReferenceEquals(e, nativeFailure)) { }
        }
        actualCycles++;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void RunCycle()
    {
        current = new RuntimeGeneration(payload, new[] { typeof(IRuntimeModule).Assembly, typeof(Node).Assembly, typeof(Gate).Assembly });
        var panel = (Control)current.Invoke("ui", counter)!;
        AddChild(panel);
        if (panel.GetChild<Label>(0).Text != "Reload " + counter) throw new Exception("Runtime UI did not activate");
        if (Target(cycles) != cycles + 7) throw new Exception("Stable Harmony entry did not route to runtime");
        if (Harmony.GetPatchInfo(typeof(Gate).GetMethod(nameof(Target))!)!.Owners.Count != 1) throw new Exception("Duplicate patches");
        RemoveChild(panel); panel.Free();
        current.Dispose(); retired.Add(current.RetiredContext!); current = null;
        cycles++; counter++;
    }

    private void Fail(Exception e)
    {
        failed = true;
        GD.PrintErr(e); if (report.Length > 0) File.WriteAllText(report, e.ToString());
        patches.UnpatchAll(patches.Id); current?.Dispose(); current = null; GetTree().Quit(1);
    }
}
