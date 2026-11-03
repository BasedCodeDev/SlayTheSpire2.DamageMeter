using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Runtime.Loader;
using Based.Spires2DamageMeter.Hosting;
using Based.Spires2DamageMeter.Reload;

static class HotReloadChecks
{
    public static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "damage-meter-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(root.FullName, "Based.Spires2DamageMeter.csproj"))) root = root.Parent!;
            var payload = Path.Combine(root.FullName, "runtime/bin/Release/net9.0/" + BuildManifest.PayloadName);
            File.Copy(typeof(RuntimeHost).Assembly.Location, Path.Combine(directory, "Based.Spires2DamageMeter.dll"));
            File.Copy(typeof(RuntimeContract).Assembly.Location, Path.Combine(directory, "Based.Spires2DamageMeter.Contracts.dll"));
            var files = new Dictionary<string, string> { [BuildManifest.PayloadName] = BuildManifest.HashFile(payload) };
            var manifest = new BuildManifest(BuildManifest.ContentId(files), "0.3.0-preview.25",
                BuildManifest.HashFile(typeof(RuntimeHost).Assembly.Location), BuildManifest.HashFile(typeof(RuntimeContract).Assembly.Location),
                RuntimeContract.ApiVersion, RuntimeContract.StateVersion, files);
            var build = Path.Combine(directory, "runtime/builds", manifest.BuildId);
            Directory.CreateDirectory(build);
            var published = Path.Combine(build, BuildManifest.PayloadName);
            File.Copy(payload, published);
            check(Path.GetFullPath(manifest.Validate(directory)) == Path.GetFullPath(published), "runtime manifest validates exact host/contracts/payload content");
            var pointer = Path.Combine(directory, "runtime/current.json");
            File.WriteAllText(pointer, JsonSerializer.Serialize(manifest));
            check(BuildManifest.Read(pointer).BuildId == manifest.BuildId, "runtime manifest roundtrip preserves build identity");
            void Reject(Action action, string name)
            {
                try { action(); check(false, name); }
                catch (Exception e) when (e is InvalidDataException or JsonException or FileNotFoundException) { check(true, name); }
            }
            Reject(() => (manifest with { ApiVersion = 999 }).Validate(directory), "runtime rejects changed API");
            Reject(() => (manifest with { StateVersion = 999 }).Validate(directory), "runtime rejects incompatible live state");
            Reject(() => (manifest with { HostHash = new string('0', 64) }).Validate(directory), "runtime rejects changed permanent host");
            Reject(() => (manifest with { ContractsHash = new string('0', 64) }).Validate(directory), "runtime rejects changed shared contract");
            Reject(() => (manifest with { Version = "0.3.0-preview.17" }).Validate(directory), "runtime rejects withdrawn preview.17");
            Reject(() => (manifest with { BuildId = "../outside" }).Validate(directory), "runtime rejects invalid build directory identity");
            File.AppendAllText(published, "incomplete update");
            Reject(() => manifest.Validate(directory), "runtime rejects incomplete or modified DLL before loading");
            File.WriteAllText(pointer, "{");
            Reject(() => BuildManifest.Read(pointer), "runtime rejects malformed publication");
            var weak = InspectRuntime(payload, check);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            check(!weak.IsAlive, "production runtime unloads after metadata inspection");
            var bootstrap = InspectBootstrap(check);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            check(!bootstrap.IsAlive, "bootstrap metadata inspection leaves no assembly context rooted");
            var diagnostic = new
            {
                text = "HP / Block \u25a0 \"quoted\"",
                number = 12.375m,
                missing = (string?)null,
                destination = Based.Spires2DamageMeter.TooltipTone.Block,
                date = new DateTime(2026, 11, 3, 12, 34, 56, DateTimeKind.Utc),
                bytes = new byte[] { 1, 2, 3 },
                rows = new Dictionary<string, object?> { ["A"] = new Based.Spires2DamageMeter.Totals { Hp = 9, Block = 3 } },
                providers = new[] { new Based.Spires2DamageMeter.Share("A", "Strike", 9) }
            };
            check(LogValue.Freeze(diagnostic).GetRawText() == JsonSerializer.Serialize(diagnostic), "development log snapshots preserve diagnostic JSON shape, numbers and escaping");
            var cycle = new List<object>(); cycle.Add(cycle);
            Reject(() => LogValue.Freeze(cycle), "development log snapshots reject cycles");
        }
        finally { Directory.Delete(directory, true); }
    }

    private sealed class BootstrapContext() : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == "Based.Spires2DamageMeter.Contracts") throw new FileNotFoundException("Contracts must be loaded by the mod bootstrap");
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsCollectible && !a.IsDynamic && AssemblyName.ReferenceMatchesDefinition(a.GetName(), name));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference InspectBootstrap(Action<bool, string> check)
    {
        var context = new BootstrapContext();
        var host = context.LoadFromAssemblyPath(typeof(RuntimeHost).Assembly.Location);
        var types = host.GetTypes();
        var initializers = types.Where(t => t.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "MegaCrit.Sts2.Core.Modding.ModInitializerAttribute")).ToArray();
        check(initializers.Length == 1 && initializers[0].FullName == "Based.Spires2DamageMeter.Hosting.ModBootstrap", "game can discover the sole bootstrap before resolving contracts");
        var weak = new WeakReference(context);
        context.Unload();
        return weak;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference InspectRuntime(string payload, Action<bool, string> check)
    {
        var shared = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !a.IsCollectible).ToArray();
        var generation = new RuntimeGeneration(payload, shared);
        var module = (IRuntimeModule)typeof(RuntimeGeneration).GetField("module", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(generation)!;
        var types = module.GetType().Assembly.GetTypes();
        check(types.All(t => !typeof(Godot.GodotObject).IsAssignableFrom(t)), "reloadable payload declares no Godot script subclasses");
        check(types.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            .All(m => m.GetCustomAttribute<AsyncStateMachineAttribute>() == null), "reloadable payload owns no async state machines");
        generation.Dispose();
        return generation.RetiredContext!;
    }
}
