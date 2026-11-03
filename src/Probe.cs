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

[ModInitializer(nameof(Initialize))]
public static class Probe
{
    public const string Id = "Based.Spires2DamageMeter";
    public static readonly Accounting Meter = new();
    public static string Health = "Starting";
    public static string Role = "unknown";
    public static string Combat = "No combat";
    public static readonly Dictionary<string, string> Names = new();
    private static readonly PowerChangeTracker changes = new();
    private static readonly Dictionary<Creature, (int Hp, object Token, IReadOnlyList<Share> Shares)> doom = new();
    private sealed record Origin(string? Owner, string? Source, string OperationId);
    private static readonly SourceContext<Origin> origin = new();
    public static bool OwnershipAvailable { get; private set; }
    public static string OwnershipStatus => $"Ownership: {(OwnershipAvailable ? "enabled" : "disabled")}; changes {changes.Confirmed}, unknown {changes.Unknown}, unresolved {changes.Unresolved}";
    private static string gameVersion = "unknown";
    public static string Version => typeof(Probe).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> queue = new();
    private static readonly AsyncLocal<PowerModel?> poison = new();
    private static StreamWriter? writer;
    private static System.Threading.Timer? timer;
    private static Harmony? harmony;
    private static readonly object logLock = new();
    private static long sequence;
    private static int combatIndex;
    private static bool active;
    private static readonly string session = Guid.NewGuid().ToString("N");

    public static object? Get(object? obj, string name) => obj?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
    private static object? Arg(MethodBase method, object[] args, string name) { var i = Array.FindIndex(method.GetParameters(), p => p.Name == name); return i < 0 ? null : args[i]; }
    private static string? Owner(object? obj) => obj switch { Player p => p.NetId.ToString(), Creature c => c.Player?.NetId.ToString(), CardModel c => c.Owner?.NetId.ToString(), _ => null };
    private static string Entity(object? obj) => obj == null ? "unknown" : $"{obj.GetType().Name}:{Get(obj, "CombatId") ?? Get(obj, "NetId") ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj)}";

    public static void Initialize()
    {
        if (harmony != null) return;
        try
        {
            var dir = Path.Combine(OS.GetUserDataDir(), Id, "logs"); Directory.CreateDirectory(dir);
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
                if (version != "v0.107.1") Health = "INCOMPLETE: unverified game version " + version;
                Emit("game_version", new { version });
            }
            harmony = new Harmony(Id);
            foreach (var name in new[] { "BeforeCombatStart", "AfterCombatEnd", "AfterDamageGiven", "AfterPowerAmountChanged", "AfterDiedToDoom" })
                Patch(typeof(Hook), name, nameof(HookPrefix), nameof(HookPostfix));
            Patch(typeof(PoisonPower), nameof(PoisonPower.AfterSideTurnStart), nameof(PoisonPrefix), nameof(PoisonPostfix));
            Patch(typeof(DoomPower), nameof(DoomPower.DoomKill), nameof(DoomPrefix), nameof(DoomPostfix));
            RegisterLedgerHooks();
            NativeDamageCapture.Register();
            RunSession.Register();
            Emit("startup", new { modVersion = Version, assembly = typeof(Hook).Assembly.FullName, supportedGame = "v0.107.1", logDirectory = dir });
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static void Patch(Type type, string name, string? prefix, string? postfix)
    {
        try
        {
            var method = AccessTools.Method(type, name) ?? throw new MissingMethodException(type.FullName, name);
            harmony!.Patch(method, prefix: prefix == null ? null : new HarmonyMethod(typeof(Probe), prefix), postfix: postfix == null ? null : new HarmonyMethod(typeof(Probe), postfix), finalizer: type == typeof(PoisonPower) ? new HarmonyMethod(typeof(Probe), nameof(PoisonFinalizer)) : type == typeof(DoomPower) ? new HarmonyMethod(typeof(Probe), nameof(DoomFinalizer)) : null);
            Emit("hook_registered", new { method = method.ToString() });
        }
        catch (Exception ex) { Fail(ex); }
    }

    public static void HookPrefix(MethodBase __originalMethod, object[] __args)
    {
        if (__originalMethod.Name != "BeforeCombatStart") return;
        Guard(() =>
        {
            Meter.Reset(); NativeDamageCapture.Reset(); changes.Clear(); doom.Clear(); Names.Clear(); active = true;
            Combat = $"Fight {++combatIndex}";
            var state = Arg(__originalMethod, __args, "runState");
            RunSession.AttachState(state);
            RunSession.Encounter = $"{Get(state, "CurrentActIndex")}:{Get(state, "TotalFloor")}:{Get(state, "CurrentRoomCount")}";
            RunSession.Active = true;
            if (Get(state, "Players") is IEnumerable players)
                foreach (var item in players) if (item is Player p) { Names[p.NetId.ToString()] = p.Creature.Name; Meter.Rows.TryAdd(p.NetId.ToString(), new()); }
            var manager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            var instance = manager?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            Role = Get(Get(instance, "NetService"), "Type")?.ToString() ?? "unknown";
            Emit("combat_start", new { players = Names, role = Role, seed = Get(Get(state, "Rng"), "StringSeed") });
            MeterPanel.Ensure();
        });
    }

    public static void HookPostfix(MethodBase __originalMethod, object[] __args, ref Task __result)
    {
        if (__originalMethod.Name == "AfterCombatEnd") { __result = EndCombat(__result); return; }
        Guard(() =>
        {
            var m = __originalMethod;
            if (!active) return;
            if (m.Name == "AfterDamageGiven" && Arg(m, __args, "results") is DamageResult r)
            {
                if(!NativeDamageCapture.Observe(r,out var eventId))return;
                var target = Arg(m, __args, "target") as Creature;
                var dealer = Arg(m, __args, "dealer"); var card = Arg(m, __args, "cardSource");
                var kind = dealer == null && card == null && poison.Value != null && ReferenceEquals(poison.Value.Owner, target) ? "Poison" : "Direct";
                var owner = Owner(dealer) ?? Owner(card) ?? "Unattributed";
                var normalized = EngineDamage.Normalize(r.UnblockedDamage,r.BlockedDamage,r.OverkillDamage);
                var hp = normalized.Hp;
                var receiver = r.Receiver;
                var shares = kind == "Poison" ? StatusOwnershipLedger.Allocate(Ownership(poison.Value!), hp) : new[] { new Share(owner, card?.GetType().Name ?? "Unknown source", hp) };
                var damageSource = new SourceIdentity(kind=="Direct"?"card":"power", card is CardModel sourceCard?sourceCard.Id.Entry:kind,card?.GetType().Name??kind);
                if (receiver.Side.ToString() == "Enemy" && !Meter.AddResolved(new(r, kind, hp, r.BlockedDamage, shares){Source=kind=="Direct"?damageSource:null})) return;
                var amplification = target != null ? NativeDamageCapture.Resolve(target,r,eventId) : null;
                if(receiver.Side.ToString()=="Enemy" && amplification != null)
                {
                    if(kind=="Direct")Meter.ApplyAmplification(owner,amplification,damageSource);
                    else Meter.ApplySharedAmplification(kind,shares,amplification);
                }

                Emit("damage_allocation", new { eventId, target = Entity(target), kind, hp, shares, trigger = origin.Current });
                Emit("damage", new { eventId, originalTarget = Entity(target), receiver = Entity(receiver), target = Entity(target), actor = Entity(dealer), owner, source = card?.GetType().Name ?? poison.Value?.GetType().Name, kind, confidence = shares.Any(s => s.Owner == StatusOwnershipLedger.Unknown) ? "partial_or_unknown" : "observed", reason = shares.Any(s => s.Owner == StatusOwnershipLedger.Unknown) ? "missing history or unresolved correlation" : null, hp = normalized.Hp, rawUnblocked = r.UnblockedDamage, blocked = r.BlockedDamage, overkill = r.OverkillDamage, enemy = receiver.Side.ToString() == "Enemy" });
            }
            if (m.Name == "AfterPowerAmountChanged" && Arg(m, __args, "power") is PowerModel power && power.GetType().Name is "PoisonPower" or "DoomPower" or "VulnerablePower")
            {
                var applier = Arg(m, __args, "applier"); var delta = Convert.ToDecimal(Arg(m, __args, "amount") ?? 0);
                Emit("power_change", new { effect = power.GetType().Name, target = Entity(power.Owner), after = power.Amount, reportedDelta = delta, applier = Owner(applier), source = Arg(m, __args, "cardSource")?.GetType().Name, shares = Ownership(power) });
            }
            if (m.Name == "AfterDiedToDoom" && Arg(m, __args, "creatures") is IEnumerable victims)
                foreach (var item in victims) if (item is Creature c && doom.Remove(c, out var d))
                {
                    var shares = StatusOwnershipLedger.Allocate(d.Shares, d.Hp);
                    if (c.Side.ToString() == "Enemy" && !Meter.AddResolved(new(d.Token, "Doom", d.Hp, 0, shares))) continue;
                    Emit("doom_execution", new { eventId=Guid.NewGuid().ToString("N"), enemy=c.Side.ToString()=="Enemy", target = Entity(c), hp = d.Hp, shares });
                }
        });
    }
    private static async Task EndCombat(Task task)
    {
        try { await task; }
        finally { Guard(() => { RunSession.Commit(); RunSession.Active = false; Emit("combat_summary", new { totals = Meter.Rows, sources = Meter.Sources, confirmedChanges = changes.Confirmed, unknownChanges = changes.Unknown, unresolvedCorrelations = changes.Unresolved, health = Health, offensiveCoverage = NativeDamageCapture.Coverage.Outgoing, incomingDiagnostics = NativeDamageCapture.Coverage.Incoming }); active = false; Combat += " complete"; doom.Clear(); changes.Clear(); Flush(); }); }
    }
    private static void RegisterLedgerHooks()
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
                var postfix = method.ReturnType == typeof(Task)
                    ? typeof(Probe).GetMethod(nameof(SourceTaskPostfix))!
                    : typeof(Probe).GetMethod(nameof(SourcePostfix))!.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0]);
                return new(method, H(nameof(SourcePrefix)), new HarmonyMethod(postfix), H(nameof(SourceFinalizer)));
            }
            var specs = new[] { new PatchSpec(methods[0], Prefix: H(nameof(BeforePowerPrefix))), new PatchSpec(methods[1], Prefix: H(nameof(AfterPowerPrefix))), Source(methods[2]), Source(methods[3]), new PatchSpec(methods[4], Prefix: H(nameof(RemovePrefix)), Postfix: H(nameof(RemovePostfix))) };
            foreach (var method in methods) Emit("ownership_target_validated", new { signature = method.ToString(), declaringType = method.DeclaringType?.FullName });
            OwnershipHooks.Install(ownershipHarmony, specs, method => { target = method.ToString(); Emit("ownership_target_installed", new { signature = target }); });
            OwnershipAvailable = true;
            Emit("ownership_registration", new { enabled = true, expected = methods.Length, installed = methods.Length, targets = methods.Select(m => m.ToString()).ToArray(), gameVersion, modVersion = Version });
        }
        catch (Exception ex)
        {
            ownershipHarmony.UnpatchAll(ownershipHarmony.Id); changes.Clear();
            Fail(new InvalidOperationException($"Ownership disabled; target {target ?? "preflight"}: {ex.Message}", ex));
            Emit("ownership_registration", new { enabled = false, expectedTargets = "Before/AfterPowerAmountChanged, Apply, ModifyAmount, Remove", installed = 0, error = ex.ToString(), gameVersion, modVersion = Version });
        }
    }
    public static IReadOnlyList<Share> PowerOwnership(PowerModel power) => Ownership(power);
    private static IReadOnlyList<Share> Ownership(PowerModel power) => OwnershipAvailable ? changes.Snapshot(PowerKey(power), power.Amount) : Array.Empty<Share>();
    private static string PowerKey(PowerModel power) => Entity(power.Owner) + ":" + power.GetType().FullName;
    public static void BeforePowerPrefix(PowerModel power, Creature target, Creature? applier, CardModel? cardSource) => Guard(() =>
    {
        if (!active || !OwnershipAvailable) return;
        var existing = target.Powers.FirstOrDefault(p => p.GetType() == power.GetType());
        var operation = origin.Current?.OperationId ?? "automatic";
        changes.Begin(operation, Entity(target) + ":" + power.GetType().FullName, existing?.Amount ?? 0, Owner(applier) ?? Owner(cardSource), cardSource?.GetType().Name);
    });
    public static void AfterPowerPrefix(PowerModel power, Creature? applier, CardModel? cardSource) => Guard(() =>
    {
        if (!active || !OwnershipAvailable) return;
        var change = changes.End(origin.Current?.OperationId ?? "automatic", PowerKey(power), power.Amount, Owner(applier) ?? Owner(cardSource), cardSource?.GetType().Name);
        Emit(change.Quality is "duplicate" or "unmatched" ? "ownership_observation" : "ownership_change", change);
    });
    public static void SourcePrefix(MethodBase __originalMethod, object[] __args, out object? __state)
    {
        __state = origin.Current;
        origin.Current = null;
        Guard(() =>
        {
            var card = Arg(__originalMethod, __args, "cardSource");
            origin.Current = new(Owner(Arg(__originalMethod, __args, "applier")) ?? Owner(card), card?.GetType().Name, Guid.NewGuid().ToString("N"));
        });
    }
    public static void SourcePostfix<T>(ref Task<T> __result, object? __state)
    {
        var current = origin.Current; origin.Current = (Origin?)__state;
        __result = FinishSource(__result, current, (Origin?)__state);
    }
    public static void SourceTaskPostfix(ref Task __result, object? __state)
    {
        var current = origin.Current; origin.Current = (Origin?)__state;
        __result = FinishSource(__result, current, (Origin?)__state);
    }
    private static async Task FinishSource(Task task, Origin? current, Origin? previous)
    { try { await origin.Complete(task, current, previous); } finally { if (current != null) Guard(() => changes.EndScope(current.OperationId)); } }
    private static async Task<T> FinishSource<T>(Task<T> task, Origin? current, Origin? previous)
    { try { return await origin.Complete(task, current, previous); } finally { if (current != null) Guard(() => changes.EndScope(current.OperationId)); } }
    public static Exception? SourceFinalizer(Exception? __exception, object? __state) { if (__exception != null && origin.Current != null) Guard(() => changes.EndScope(origin.Current.OperationId)); origin.Current = (Origin?)__state; return __exception; }
    public static void RemovePrefix(PowerModel power, out string __state) => __state = PowerKey(power);
    public static void RemovePostfix(ref Task __result, string __state) => __result = CompleteRemove(__result, __state);
    private static async Task CompleteRemove(Task task, string key) { await task; Guard(() => changes.Remove(key)); }
    public static void DoomPrefix(IEnumerable<Creature> creatures, out object __state)
    {
        var tokens = new Dictionary<Creature, object>(); __state = tokens;
        Guard(() => { foreach (var c in creatures) { var p = c.GetPower<DoomPower>(); if (c.CurrentHp > 0 && p != null) { var token = new object(); tokens[c] = token; doom[c] = (c.CurrentHp, token, Ownership(p)); } } });
    }
    private static void CleanDoom(object state) => Guard(() => { foreach (var pair in (Dictionary<Creature, object>)state) if (doom.TryGetValue(pair.Key, out var entry) && ReferenceEquals(entry.Token, pair.Value)) doom.Remove(pair.Key); });
    public static void DoomPostfix(ref Task __result, object __state) => __result = CompleteDoom(__result, __state);
    private static async Task CompleteDoom(Task task, object state) { try { await task; } finally { CleanDoom(state); } }
    public static Exception? DoomFinalizer(Exception? __exception, object __state) { if (__exception != null && __state != null) CleanDoom(__state); return __exception; }
    public static void PoisonPrefix(PoisonPower __instance, out PowerModel? __state) { __state = poison.Value; poison.Value = __instance; }
    public static void PoisonPostfix(ref Task __result, PowerModel? __state) { var current = poison.Value; poison.Value = __state; __result = ObservePoison(__result, current, __state); }
    public static Exception? PoisonFinalizer(Exception? __exception, PowerModel? __state) { poison.Value = __state; return __exception; }
    private static async Task ObservePoison(Task task, PowerModel? current, PowerModel? previous) { poison.Value = current; try { await task; } finally { poison.Value = previous; } }
    private static void Guard(Action action) { try { action(); } catch (Exception ex) { Fail(ex); } }
    private static void Fail(Exception ex) { Health = "INCOMPLETE: " + ex.Message; GD.PrintErr(Id + ": " + ex); Emit("capture_error", new { error = ex.ToString() }); }
    public static void Emit(string kind, object data) { try { queue.Enqueue(JsonSerializer.Serialize(new { schemaVersion = 6, amplificationVersion=Amplification.Version, accountingVersion = EngineDamage.AccountingVersion, session, combat = combatIndex, sequence = Interlocked.Increment(ref sequence), utc = DateTime.UtcNow, role = Role, kind, data })); } catch { Health = "INCOMPLETE: serialization failure"; } }
    public static void Flush() { lock (logLock) { try { while (queue.TryDequeue(out var line)) writer?.WriteLine(line); writer?.Flush(); } catch (Exception ex) { Health = "INCOMPLETE: log write: " + ex.Message; } } }
}
