using System.Reflection;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Modding;

namespace Based.Spires2DamageMeter.Hosting;

[ModInitializer(nameof(Initialize))]
public static class ModBootstrap
{
    public static void Initialize()
    {
        // Resolve the shared contract before the JIT touches any typed runtime entry point.
        var host = typeof(ModBootstrap).Assembly;
        var owner = AssemblyLoadContext.GetLoadContext(host)!;
        owner.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(host.Location)!, "Based.Spires2DamageMeter.Contracts.dll"));
        host.GetType("Based.Spires2DamageMeter.Hosting.RuntimeHost")!.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }
}
