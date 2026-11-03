using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Based.Spires2DamageMeter.Reload;

namespace Based.Spires2DamageMeter.Hosting;

/// <summary>Loads only payload assemblies privately; engine and contract identity stays shared.</summary>
public sealed class RuntimeGeneration : IDisposable
{
    private sealed class PayloadContext(string path, Assembly[] shared) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly string directory = Path.GetDirectoryName(path)!;
        private readonly AssemblyLoadContext owner = GetLoadContext(typeof(RuntimeGeneration).Assembly)!;
        protected override Assembly? Load(AssemblyName name)
        {
            var existing = shared.FirstOrDefault(a => AssemblyName.ReferenceMatchesDefinition(a.GetName(), name));
            if (existing != null) return existing;
            var dependency = Path.Combine(directory, name.Name + ".dll");
            if (File.Exists(dependency)) return LoadFromAssemblyPath(dependency);
            // Godot's game context resolves dependencies from sts2.deps.json. Falling
            // back to Default can create a second System.Text.Json identity: even a
            // helper's return type then prevents its MemberRef from binding.
            return owner.LoadFromAssemblyName(name);
        }
    }

    private PayloadContext? context;
    private IRuntimeModule? module;
    public WeakReference? RetiredContext { get; private set; }
    // Keep metadata separate across generations. Private payload values must first
    // pass through LogValue.Freeze; the framework's global accessor cache outlives options.
    public JsonSerializerOptions JsonOptions { get; } = new() { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
    public string Version { get; private set; } = "unknown";

    public RuntimeGeneration(string path, IEnumerable<Assembly> shared, bool collectible = true)
    {
        try
        {
            Assembly assembly;
            if (collectible)
            {
                context = new PayloadContext(Path.GetFullPath(path), shared.ToArray());
                assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
            else
            {
                var owner = AssemblyLoadContext.GetLoadContext(typeof(RuntimeGeneration).Assembly)!;
                assembly = owner.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "unknown";
            var type = assembly.GetType("Based.Spires2DamageMeter.Runtime.RuntimeModule", throwOnError: true)!;
            module = (IRuntimeModule)Activator.CreateInstance(type)!;
            if (module.ApiVersion != RuntimeContract.ApiVersion || module.StateVersion != RuntimeContract.StateVersion)
                throw new InvalidOperationException("Runtime contract changed. Restart with a matching host.");
            module.Prepare();
        }
        catch { Dispose(); throw; }
    }

    public object? Invoke(string operation, params object?[] arguments)
        => (module ?? throw new ObjectDisposedException(nameof(RuntimeGeneration))).Invoke(operation, arguments);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Dispose()
    {
        var previous = module;
        module = null;
        try { previous?.Dispose(); }
        finally
        {
            previous = null;
            if (context != null)
            {
                RetiredContext = new WeakReference(context);
                context.Unload();
                context = null;
            }
        }
    }
}
