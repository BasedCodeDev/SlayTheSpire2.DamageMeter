using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using HarmonyLib;

// Uses Godot's actual dependency resolver and the installed sts2.deps.json.
// A self-contained fixture's own deps.json does not reproduce the game's JSON identity.
public static class GameLoadingChecks
{
    public static void Run(string hostPath, string gameData, string godotPlugins, string[] payloads, Action<bool, string> check)
    {
        _ = JsonSerializer.Serialize(new { defaultContext = true });
        var previousBase = AppContext.BaseDirectory;
        var plugin = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a => a.GetName().Name == "GodotPlugins")
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(godotPlugins));
        var godot = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "GodotSharp")
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(gameData, "GodotSharp.dll"));
        var loader = plugin.GetType("GodotPlugins.PluginLoadContext", true)!;
        var owner = (AssemblyLoadContext)Activator.CreateInstance(loader, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, [Path.Combine(gameData, "sts2.dll"), new List<string> { "GodotSharp" }, AssemblyLoadContext.GetLoadContext(godot)!, true], null)!;
        var harmony = new Harmony("Based.DamageMeter.GameLoadingChecks");
        FieldInfo? generationField = null;
        object? generation = null;
        try
        {
            var game = owner.LoadFromAssemblyPath(Path.Combine(gameData, "sts2.dll"));
            var host = owner.LoadFromAssemblyPath(Path.GetFullPath(hostPath));
            owner.LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(hostPath)!, "Based.Spires2DamageMeter.Contracts.dll"));
            var hostType = host.GetType("Based.Spires2DamageMeter.Hosting.RuntimeHost", true)!;
            var capture = host.GetType("Based.Spires2DamageMeter.NativeDamageCapture", true)!;
            var creatureType = game.GetType("MegaCrit.Sts2.Core.Entities.Creatures.Creature", true)!;
            var creature = RuntimeHelpers.GetUninitializedObject(creatureType);
            var block = creatureType.GetProperty("Block")!;
            var target = creatureType.GetMethod("DamageBlockInternal", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
            harmony.Patch(target, prefix: new HarmonyMethod(capture.GetMethod("BlockPrefix")!), postfix: new HarmonyMethod(capture.GetMethod("BlockPostfix")!));
            generationField = hostType.GetField("current", BindingFlags.Static | BindingFlags.NonPublic)!;
            foreach (var payload in payloads)
            {
                hostType.GetMethod("LoadForChecks")!.Invoke(null, [payload, gameData]);
                generation = generationField.GetValue(null)!;
                var module = generation.GetType().GetField("module", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(generation)!;
                var payloadContext = AssemblyLoadContext.GetLoadContext(module.GetType().Assembly)!;
                var hostJson = host.GetType("Based.Spires2DamageMeter.Hosting.LogValue", true)!.GetMethod("Freeze")!.ReturnType.Assembly;
                var payloadJson = payloadContext.LoadFromAssemblyName(hostJson.GetName());
                Console.WriteLine($"JSON binding: host={hostJson.Location} [{AssemblyLoadContext.GetLoadContext(hostJson)?.GetType().FullName}]; payload={payloadJson.Location} [{AssemblyLoadContext.GetLoadContext(payloadJson)?.GetType().FullName}]");
                var trace = Activator.CreateInstance(capture.GetNestedType("Trace", BindingFlags.NonPublic)!, true)!;
                trace.GetType().GetField("Target")!.SetValue(trace, creature);
                var scope = Activator.CreateInstance(capture.GetNestedType("Scope", BindingFlags.NonPublic)!, true)!;
                scope.GetType().GetField("Active")!.SetValue(scope, trace);
                var slot = capture.GetField("scope", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
                slot.GetType().GetProperty("Value")!.SetValue(slot, scope);
                block.SetValue(creature, 10);
                var absorbed = target.Invoke(creature, [6m, Enum.ToObject(target.GetParameters()[1].ParameterType, 0)]);
                check((decimal)absorbed! == 6 && (int)block.GetValue(creature)! == 4, "game-loader block callback preserves real native damage result and block");
                var phases = (IList)trace.GetType().GetField("Phases")!.GetValue(trace)!;
                check(phases.Count == 1, "game-loader block callback records its logging phase before and after runtime replacement");
                using var json = JsonDocument.Parse(phases[0]!.ToString()!);
                check(json.RootElement.GetProperty("absorbed").GetDecimal() == 6 && json.RootElement.GetProperty("before").GetInt32() == 10,
                    "game-loader block phase preserves JSON contents");
                check(ReferenceEquals(hostJson, payloadJson) && !ReferenceEquals(hostJson, typeof(JsonElement).Assembly),
                    "runtime shares the game's JSON identity even when the default context has a separate copy");
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            (generation as IDisposable)?.Dispose();
            generationField?.SetValue(null, null);
            owner.Unload();
            AppContext.SetData("APP_CONTEXT_BASE_DIRECTORY", previousBase);
        }
    }
}
