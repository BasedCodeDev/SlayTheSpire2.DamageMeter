using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using Based.Spires2DamageMeter.Reload;
using Godot;

namespace Based.Spires2DamageMeter.Hosting;

public static class RuntimeHost
{
#if DEVELOPMENT_HOT_RELOAD
    public static bool Development => true;
#else
    public static bool Development => false;
#endif
    private static readonly object gate = new();
    [ThreadStatic] private static RuntimeGeneration? dispatching;
    private static RuntimeGeneration? current;
    private static FileSystemWatcher? watcher;
    private static string installation = "", manifestPath = "", lastAttempt = "";
    private static int requested = 1;
    private static double pollElapsed;
    private static bool stopped;
    private static readonly List<WeakReference> retired = new();
    private static readonly Dictionary<long, (RuntimeGeneration Owner, Action Callback)> deferred = new();
    private static readonly Dictionary<RuntimeGeneration, List<MeterPanel>> ownedUi = new();
    private static long deferredId;
    public static string BuildId { get; private set; } = "unloaded";
    public static string Version { get; private set; } = "unloaded";
    public static string Status { get; private set; } = "Runtime not loaded";
    internal static int RetainedGenerationCount => retired.Count(r => r.IsAlive);
    internal static ReloadDriver? Driver;
    public static JsonSerializerOptions JsonOptions => (dispatching ?? current)?.JsonOptions ?? new JsonSerializerOptions();

    public static void Start() => StartCore(Path.GetDirectoryName(typeof(RuntimeHost).Assembly.Location)!, true);

    internal static void StartNativeChecks(string directory) => StartCore(directory, false);

    private static void StartCore(string directory, bool installCapture)
    {
        lock (gate)
        {
            if (current != null) return;
            installation = directory;
            manifestPath = Path.Combine(installation, "runtime", "current.json");
            RuntimeCalls.Handler = Call;
            var manifest = BuildManifest.Read(manifestPath);
            var payload = manifest.Validate(installation);
            current = new RuntimeGeneration(payload, SharedAssemblies(), Development);
            BuildId = manifest.BuildId; Version = manifest.Version;
            Status = Development ? "Development runtime " + BuildId[..12] : "Runtime " + Version;
            if (Development)
            {
                watcher = new FileSystemWatcher(Path.GetDirectoryName(manifestPath)!, "current.json")
                { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size };
                watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed;
                watcher.Renamed += (_, _) => Interlocked.Exchange(ref requested, 1);
                watcher.Error += (_, _) => Interlocked.Exchange(ref requested, 1);
                watcher.EnableRaisingEvents = true;
            }
            AppDomain.CurrentDomain.ProcessExit += OnExit;
            Driver = new ReloadDriver();
            ((SceneTree)Engine.GetMainLoop()).Root.CallDeferred(Node.MethodName.AddChild, Driver);
        }
        if (installCapture) Probe.Initialize();
    }

    private static void Changed(object sender, FileSystemEventArgs args) => Interlocked.Exchange(ref requested, 1);
    private static Assembly[] SharedAssemblies() => [typeof(RuntimeHost).Assembly, typeof(IRuntimeModule).Assembly,
        typeof(Node).Assembly, typeof(HarmonyLib.Harmony).Assembly, typeof(MegaCrit.Sts2.Core.Entities.Creatures.Creature).Assembly];

    /// <summary>Loads real production logic without initializing Godot or installing game hooks.</summary>
    public static void LoadForChecks(string payload, string gameData)
    {
        var owner = AssemblyLoadContext.GetLoadContext(typeof(RuntimeHost).Assembly)!;
        owner.Resolving += (_, name) =>
        {
            var path = Path.Combine(gameData, name.Name + ".dll");
            return File.Exists(path) ? owner.LoadFromAssemblyPath(path) : null;
        };
        lock (gate)
        {
            current?.Dispose();
            current = new RuntimeGeneration(payload, SharedAssemblies());
            RuntimeCalls.Handler = Call;
            Version = current.Version;
        }
    }

    public static void LoadBuiltRuntimeForChecks(string? gameData = null)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Based.Spires2DamageMeter.csproj"))) directory = directory.Parent;
        if (directory == null) throw new DirectoryNotFoundException("Cannot locate the damage meter source tree");
        LoadForChecks(Path.Combine(directory.FullName, "runtime/bin/Release/net9.0/" + BuildManifest.PayloadName),
            gameData ?? System.Environment.GetEnvironmentVariable("STS2_DATA_DIR") ?? @"C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64");
    }

    private static object? Call(string operation, object?[] arguments)
    {
        lock (gate)
        {
            var target = dispatching ?? current ?? throw new InvalidOperationException("Damage meter runtime unavailable");
            return InGeneration(target, operation, arguments);
        }
    }

    private static object? InGeneration(RuntimeGeneration generation, string operation, params object?[] arguments)
    {
        var previous = dispatching; dispatching = generation;
        try { return generation.Invoke(operation, arguments); }
        finally { dispatching = previous; }
    }

    public static void Defer(Action callback)
    {
        lock (gate)
        {
            if (Driver == null) return;
            var id = ++deferredId;
            deferred[id] = (dispatching ?? current!, callback);
            Driver.CallDeferred(ReloadDriver.MethodName.RunDeferred, id);
        }
    }

    internal static void TrackUi(MeterPanel panel)
    {
        lock (gate)
        {
            var owner = dispatching ?? current;
            if (owner == null) return;
            if (!ownedUi.TryGetValue(owner, out var panels)) ownedUi[owner] = panels = new();
            panels.Add(panel);
        }
    }

    internal static void RunDeferred(long id)
    {
        lock (gate)
        {
            if (!deferred.Remove(id, out var work) || !ReferenceEquals(work.Owner, current)) return;
            var previous = dispatching; dispatching = work.Owner;
            try { work.Callback(); }
            catch (Exception e) { Report("Deferred UI update failed", e); }
            finally { dispatching = previous; }
        }
    }

    public static void Pump(double delta)
    {
        if (!Development || stopped) return;
        pollElapsed += delta;
        if (pollElapsed >= 1) { pollElapsed = 0; Interlocked.Exchange(ref requested, 1); }
        if (Volatile.Read(ref requested) == 0 || !Monitor.TryEnter(gate)) return;
        try
        {
            Interlocked.Exchange(ref requested, 0);
            var attempt = File.GetLastWriteTimeUtc(manifestPath).Ticks.ToString();
            if (attempt == lastAttempt) return;
            lastAttempt = attempt;
            var manifest = BuildManifest.Read(manifestPath);
            if (manifest.BuildId == BuildId) return;
            var payload = manifest.Validate(installation);
            CollectRetired();
            if (retired.Count(r => r.IsAlive) > 2)
            {
                stopped = true;
                throw new InvalidOperationException("Retired runtimes remain rooted. Restart required; further reloads stopped.");
            }
            Apply(manifest, payload);
        }
        catch (Exception e) { Report("Reload rejected; previous runtime retained", e); }
        finally { Monitor.Exit(gate); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Apply(BuildManifest manifest, string payload)
    {
        var previous = current!;
        RuntimeGeneration? candidate = null;
        MeterPanel? nextUi = null;
        var oldUi = MeterPanel.instance;
        var layout = oldUi?.CapturePresentation();
        var root = (SceneTree)Engine.GetMainLoop();
        var committed = false;
        try
        {
            candidate = new RuntimeGeneration(payload, SharedAssemblies());
            if (oldUi != null)
            {
                nextUi = (MeterPanel)InGeneration(candidate, "Ui.Prepare", layout)!;
                if (!GodotObject.IsInstanceValid(nextUi) || !nextUi!.Initialized || nextUi.GetParent() != null ||
                    !ownedUi.TryGetValue(candidate, out var panels) || !panels.Contains(nextUi))
                    throw new InvalidOperationException("Replacement UI was not prepared as an owned, detached meter.");
            }
            if (BuildManifest.Read(manifestPath).BuildId != manifest.BuildId)
            { Interlocked.Exchange(ref requested, 1); lastAttempt = ""; return; }
            if (oldUi?.GetParent() != null) oldUi.GetParent().RemoveChild(oldUi);
            current = candidate;
            MeterPanel.instance = nextUi;
            if (nextUi != null)
            {
                root.Root.AddChild(nextUi);
                if (nextUi.GetParent() != root.Root || !nextUi.IsInsideTree()) throw new InvalidOperationException("Replacement UI could not attach to the game.");
            }
            Version = manifest.Version; BuildId = manifest.BuildId;
            Status = "Development runtime " + BuildId[..12];
            candidate = null;
            committed = true;
        }
        catch when (!committed)
        {
            if (nextUi?.GetParent() != null) nextUi.GetParent().RemoveChild(nextUi);
            current = previous;
            MeterPanel.instance = oldUi;
            if (oldUi != null && oldUi.GetParent() == null) root.Root.AddChild(oldUi);
            throw;
        }
        finally
        {
            if (candidate != null)
            {
                Retire(candidate);
            }
        }
        if (committed)
        {
            try { DestroyUi(oldUi); Retire(previous); }
            catch (Exception e) { stopped = true; Report("New runtime active; cleanup failed, restart required", e); }
            Probe.Emit("runtime_reloaded", new { build = BuildId, version = Version });
            GD.Print(Probe.Id + ": " + Status);
        }
    }

    private static void DestroyUi(MeterPanel? ui)
    {
        if (!GodotObject.IsInstanceValid(ui)) return;
        ui!.DestroyTooltips();
        ui.Free();
    }

    private static void Retire(RuntimeGeneration generation)
    {
        foreach (var id in deferred.Where(p => ReferenceEquals(p.Value.Owner, generation)).Select(p => p.Key).ToArray()) deferred.Remove(id);
        var previous = dispatching; dispatching = generation;
        try
        {
            if (ownedUi.Remove(generation, out var panels)) foreach (var panel in panels) DestroyUi(panel);
        }
        finally
        {
            dispatching = previous;
            generation.Dispose();
            if (generation.RetiredContext != null) retired.Add(generation.RetiredContext);
        }
    }

    public static void CollectRetired()
    {
        // Development diagnostic only; never force collections on the release/frame path.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        retired.RemoveAll(r => !r.IsAlive);
    }

    private static void Report(string message, Exception e)
    {
        Status = message + ": " + e.Message;
        GD.PrintErr(Probe.Id + ": " + Status);
        Probe.Emit("runtime_reload_error", new { status = Status });
    }

    private static void OnExit(object? sender, EventArgs args)
    {
        watcher?.Dispose();
        Probe.Flush();
    }
}
