using System.Collections;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace Based.Spires2DamageMeter;
public static class Probe
{
    public const string Id = "Based.Spires2DamageMeter";
    public static readonly Accounting Meter = new();
    public static readonly CreditedDamage Credited = new();
    public static readonly EffectiveDamageLedger Effective = new();
    public static string Health = "Starting";
    public static string Role = "unknown";
    public static string Combat = "No combat";
    public static readonly Dictionary<string, string> Names = new();
    public static readonly Dictionary<string, Texture2D?> CharacterIcons=new();
    public static readonly Dictionary<string,(Color Hp,Color Amp)> CharacterColors=new();
    internal static readonly PowerChangeTracker changes = new();
    internal static readonly Dictionary<Creature, (int Hp, object Token, IReadOnlyList<Share> Shares)> doom = new();
    internal sealed record Origin(string? Owner, string? Source, string OperationId);
    internal static readonly SourceContext<Origin> origin = new();
    public static bool OwnershipAvailable { get; internal set; }
    public static string OwnershipStatus => $"Ownership: {(OwnershipAvailable ? "enabled" : "disabled")}; changes {changes.Confirmed}, unknown {changes.Unknown}, unresolved {changes.Unresolved}";

    internal static string gameVersion = "unknown";
    public static string Version => Hosting.RuntimeHost.Version;

    internal static readonly System.Collections.Concurrent.ConcurrentQueue<string> queue = new();
    internal static readonly AsyncLocal<PowerModel?> poison = new();
    internal static StreamWriter? writer;
    internal static System.Threading.Timer? timer;
    internal static Harmony? harmony;
    internal static readonly object logLock = new();
    internal static long sequence;
    internal static int combatIndex;
    internal static bool active;
    internal static readonly string session = Guid.NewGuid().ToString("N");
    public static object? Get(object? obj, string name) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<object?>("Probe.Get", obj, name);
    internal static object? Arg(MethodBase method, object[] args, string name) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<object?>("Probe.Arg", method, args, name);
    internal static string? Owner(object? obj) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string?>("Probe.Owner", obj);
    internal static string Entity(object? obj) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("Probe.Entity", obj);
    public static void Initialize()
    {
        if (harmony != null)
            return;
        try
        {
            var dir = Path.Combine(OS.GetUserDataDir(), Id, "logs");
            Directory.CreateDirectory(dir);
            writer = new StreamWriter(Path.Combine(dir, session + ".jsonl"));
            timer = new System.Threading.Timer(_ => Flush(), null, 1000, 1000);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            Health = "Capture ready";
            var releasePath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(typeof(Hook).Assembly.Location)!, "..", "release_info.json"));
            if (File.Exists(releasePath))
            {
                using var release = JsonDocument.Parse(File.ReadAllText(releasePath));
                var version = release.RootElement.GetProperty("version").GetString();
                gameVersion = version ?? "unknown";
                if (version != "v0.107.1")
                    Health = "INCOMPLETE: unverified game version " + version;
                Emit("game_version", new { version });
            }

            harmony = new Harmony(Id);
            foreach (var name in new[]
            {
                "BeforeCombatStart",
                "AfterCombatEnd",
                "AfterDamageGiven",
                "AfterPowerAmountChanged",
                "AfterDiedToDoom"
            }

            )
                Patch(typeof(Hook), name, nameof(HookPrefix), nameof(HookPostfix));
            Patch(typeof(PoisonPower), nameof(PoisonPower.AfterSideTurnStart), nameof(PoisonPrefix), nameof(PoisonPostfix));
            Patch(typeof(DoomPower), nameof(DoomPower.DoomKill), nameof(DoomPrefix), nameof(DoomPostfix));
            RegisterLedgerHooks();
            NativeDamageCapture.Register();
            EffectiveCapture.Register();
            RunSession.Register();
            Emit("startup", new { modVersion = Version, assembly = typeof(Hook).Assembly.FullName, supportedGame = "v0.107.1", logDirectory = dir });
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    internal static void Patch(Type type, string name, string? prefix, string? postfix)
    {
        try
        {
            var method = AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
            harmony!.Patch(method, prefix: prefix == null ? null : new HarmonyMethod(typeof(Probe), prefix), postfix: postfix == null ? null : new HarmonyMethod(typeof(Probe), postfix), finalizer: type == typeof(PoisonPower) ? new HarmonyMethod(typeof(Probe), nameof(PoisonFinalizer)) : type == typeof(DoomPower) ? new HarmonyMethod(typeof(Probe), nameof(DoomFinalizer)) : null);
            Emit("hook_registered", new { method = method.ToString() });
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    public static void HookPrefix(MethodBase __originalMethod, object[] __args) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.HookPrefix", __originalMethod, __args);
    public static void HookPostfix(MethodBase __originalMethod, object[] __args, ref Task __result)
    {
        if (__originalMethod.Name == "AfterCombatEnd")
        {
            __result = EndCombat(__result);
            return;
        }

        HandleHook(__originalMethod, __args);
    }

    internal static async Task EndCombat(Task task)
    {
        try
        {
            await task;
        }
        finally
        {
            Guard(EndCombatCompleted);
        }
    }

    internal static void EndCombatCompleted() => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.EndCombatCompleted");
    internal static void RegisterLedgerHooks()
    {
        OwnershipAvailable = false;
        var ownershipHarmony = new Harmony(Id + ".Ownership");
        string? target = null;
        try
        {
            var methods = OwnershipHooks.ResolveGame(typeof(PowerModel).Assembly);
            HarmonyMethod H(string name) => new(typeof(Probe), name);
            PatchSpec Source(MethodInfo method)
            {
                var postfix = method.ReturnType == typeof(Task) ? typeof(Probe).GetMethod(nameof(SourceTaskPostfix))! : typeof(Probe).GetMethod(nameof(SourcePostfix))!.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0]);
                return new(method, H(nameof(SourcePrefix)), new HarmonyMethod(postfix), H(nameof(SourceFinalizer)));
            }

            var specs = new[]
            {
                new PatchSpec(methods[0], Prefix: H(nameof(BeforePowerPrefix))),
                new PatchSpec(methods[1], Prefix: H(nameof(AfterPowerPrefix))),
                Source(methods[2]),
                Source(methods[3]),
                new PatchSpec(methods[4], Prefix: H(nameof(RemovePrefix)), Postfix: H(nameof(RemovePostfix)))
            };
            foreach (var method in methods)
                Emit("ownership_target_validated", new { signature = method.ToString(), declaringType = method.DeclaringType?.FullName });
            OwnershipHooks.Install(ownershipHarmony, specs, method =>
            {
                target = method.ToString();
                Emit("ownership_target_installed", new { signature = target });
            });
            OwnershipAvailable = true;
            Emit("ownership_registration", new { enabled = true, expected = methods.Length, installed = methods.Length, targets = methods.Select(m => m.ToString()).ToArray(), gameVersion, modVersion = Version });
        }
        catch (Exception ex)
        {
            ownershipHarmony.UnpatchAll(ownershipHarmony.Id);
            changes.Clear();
            Fail(new InvalidOperationException($"Ownership disabled; target {target ?? "preflight"}: {ex.Message}", ex));
            Emit("ownership_registration", new { enabled = false, expectedTargets = "Before/AfterPowerAmountChanged, Apply, ModifyAmount, Remove", installed = 0, error = ex.ToString(), gameVersion, modVersion = Version });
        }
    }

    public static IReadOnlyList<Share> PowerOwnership(PowerModel power) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<Share>>("Probe.PowerOwnership", power);
    internal static IReadOnlyList<Share> Ownership(PowerModel power) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<IReadOnlyList<Share>>("Probe.Ownership", power);
    internal static string PowerKey(PowerModel power) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<string>("Probe.PowerKey", power);
    public static void BeforePowerPrefix(PowerModel power, Creature target, Creature? applier, CardModel? cardSource) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.BeforePowerPrefix", power, target, applier, cardSource);
    public static void AfterPowerPrefix(PowerModel power, Creature? applier, CardModel? cardSource) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.AfterPowerPrefix", power, applier, cardSource);
    public static void SourcePrefix(MethodBase __originalMethod, object[] __args, out object? __state)
    {
        object? [] values = new object? []
        {
            __originalMethod,
            __args,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.SourcePrefix", values);
        __state = (object? )values[2]!;
    }

    public static void SourcePostfix<T>(ref Task<T> __result, object? __state)
    {
        var current = origin.Current;
        origin.Current = (Origin? )__state;
        __result = FinishSource(__result, current, (Origin? )__state);
    }

    public static void SourceTaskPostfix(ref Task __result, object? __state)
    {
        var current = origin.Current;
        origin.Current = (Origin? )__state;
        __result = FinishSource(__result, current, (Origin? )__state);
    }

    internal static async Task FinishSource(Task task, Origin? current, Origin? previous)
    {
        try
        {
            await origin.Complete(task, current, previous);
        }
        finally
        {
            if (current != null)
                Guard(() => changes.EndScope(current.OperationId));
        }
    }

    internal static async Task<T> FinishSource<T>(Task<T> task, Origin? current, Origin? previous)
    {
        try
        {
            return await origin.Complete(task, current, previous);
        }
        finally
        {
            if (current != null)
                Guard(() => changes.EndScope(current.OperationId));
        }
    }

    public static Exception? SourceFinalizer(Exception? __exception, object? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("Probe.SourceFinalizer", __exception, __state);
    public static void RemovePrefix(PowerModel power, out string __state)
    {
        object? [] values = new object? []
        {
            power,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.RemovePrefix", values);
        __state = (string)values[1]!;
    }

    public static void RemovePostfix(ref Task __result, string __state) => __result = CompleteRemove(__result, __state);
    internal static async Task CompleteRemove(Task task, string key)
    {
        await task;
        Guard(() => changes.Remove(key));
    }

    public static void DoomPrefix(IEnumerable<Creature> creatures, out object __state)
    {
        object? [] values = new object? []
        {
            creatures,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.DoomPrefix", values);
        __state = (object)values[1]!;
    }

    internal static void CleanDoom(object state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.CleanDoom", state);
    public static void DoomPostfix(ref Task __result, object __state) => __result = CompleteDoom(__result, __state);
    internal static async Task CompleteDoom(Task task, object state)
    {
        try
        {
            await task;
        }
        finally
        {
            CleanDoom(state);
        }
    }

    public static Exception? DoomFinalizer(Exception? __exception, object __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("Probe.DoomFinalizer", __exception, __state);
    public static void PoisonPrefix(PoisonPower __instance, out PowerModel? __state)
    {
        object? [] values = new object? []
        {
            __instance,
            null
        };
        global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.PoisonPrefix", values);
        __state = (PowerModel? )values[1]!;
    }

    public static void PoisonPostfix(ref Task __result, PowerModel? __state)
    {
        var current = poison.Value;
        poison.Value = __state;
        __result = ObservePoison(__result, current, __state);
    }

    public static Exception? PoisonFinalizer(Exception? __exception, PowerModel? __state) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call<Exception?>("Probe.PoisonFinalizer", __exception, __state);
    internal static async Task ObservePoison(Task task, PowerModel? current, PowerModel? previous)
    {
        poison.Value = current;
        try
        {
            await task;
        }
        finally
        {
            poison.Value = previous;
        }
    }

    internal static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Fail(ex);
        }
    }

    internal static void Fail(Exception ex)
    {
        Health = "INCOMPLETE: " + ex.Message;
        GD.PrintErr(Id + ": " + ex);
        Emit("capture_error", new { error = ex.ToString() });
    }

    public static void Emit(string kind, object data)
    {
        try
        {
            object? snapshot = Hosting.RuntimeHost.Development ? Hosting.LogValue.Freeze(data) : data;
            queue.Enqueue(JsonSerializer.Serialize(new { schemaVersion = 8, effectiveVersion = EffectiveDamageLedger.Version, attributionVersion = CreditedDamage.Version, amplificationVersion = Amplification.Version, accountingVersion = EngineDamage.AccountingVersion, session, combat = combatIndex, sequence = Interlocked.Increment(ref sequence), utc = DateTime.UtcNow, role = Role, kind, data = snapshot }, Hosting.RuntimeHost.JsonOptions));
        }
        catch
        {
            Health = "INCOMPLETE: serialization failure";
        }
    }

    public static void Flush()
    {
        lock (logLock)
        {
            try
            {
                while (queue.TryDequeue(out var line))
                    writer?.WriteLine(line);
                writer?.Flush();
            }
            catch (Exception ex)
            {
                Health = "INCOMPLETE: log write: " + ex.Message;
            }
        }
    }

    internal static void HandleHook(MethodBase __originalMethod, object[] __args) => global::Based.Spires2DamageMeter.Reload.RuntimeCalls.Call("Probe.HandleHook", __originalMethod, __args);
}
