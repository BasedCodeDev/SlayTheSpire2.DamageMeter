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
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.Probe;

namespace Based.Spires2DamageMeter.Runtime;

internal static class ProbeLogic
{
    public static object? Get(object? obj, string name) => obj?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(obj);
    public static object? Arg(MethodBase method, object[] args, string name)
    {
        var i = Array.FindIndex(method.GetParameters(), p => p.Name == name);
        return i < 0 ? null : args[i];
    }

    public static string? Owner(object? obj) => obj switch
    {
        Player p => p.NetId.ToString(),
        Creature c => c.Player?.NetId.ToString(),
        CardModel c => c.Owner?.NetId.ToString(),
        _ => null
    };
    public static string Entity(object? obj) => obj == null ? "unknown" : $"{obj.GetType().Name}:{Get(obj, "CombatId") ?? Get(obj, "NetId") ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj)}";
    public static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
    public static (Color Hp, Color Amp) CharacterBarColors(CharacterModel character)
    {
        var id = character.Id.ToString();
        var colors = id.Contains("IRONCLAD", StringComparison.OrdinalIgnoreCase) ? (Rgb(216, 108, 69), Rgb(94, 10, 7))
            : id.Contains("DEFECT", StringComparison.OrdinalIgnoreCase) ? (Rgb(89, 183, 203), Rgb(33, 99, 139))
            : id.Contains("NECROBINDER", StringComparison.OrdinalIgnoreCase) ? (Rgb(147, 20, 83), Rgb(114, 16, 67))
            : id.Contains("SILENT", StringComparison.OrdinalIgnoreCase) ? (Rgb(91, 135, 100), Rgb(44, 71, 16))
            : id.Contains("REGENT", StringComparison.OrdinalIgnoreCase) ? (Rgb(160, 58, 17), Rgb(106, 47, 0))
            : (character.NameColor, character.NameColor.Darkened(.35f));
        return (colors.Item1, colors.Item2);
    }

    public static void HookPrefix(MethodBase __originalMethod, object[] __args)
    {
        if (__originalMethod.Name != "BeforeCombatStart")
            return;
        Guard(() =>
        {
            Meter.Reset();
            Credited.Clear();
            Effective.Clear();
            NativeDamageCapture.Reset();
            changes.Clear();
            doom.Clear();
            Names.Clear(); CharacterIcons.Clear(); CharacterColors.Clear();
            active = true;
            Combat = $"Fight {++combatIndex}";
            var state = Arg(__originalMethod, __args, "runState");
            RunSession.AttachState(state);
            RunSession.Encounter = $"{Get(state, "CurrentActIndex")}:{Get(state, "TotalFloor")}:{Get(state, "CurrentRoomCount")}";
            RunSession.Active = true;
            if (Get(state, "Players") is IEnumerable players)
                foreach (var item in players)
                    if (item is Player p)
                    {
                        Names[p.NetId.ToString()] = p.Creature.Name;
                        CharacterIcons[p.NetId.ToString()] = p.Character.IconTexture; CharacterColors[p.NetId.ToString()] = CharacterBarColors(p.Character);
                        Meter.Rows.TryAdd(p.NetId.ToString(), new());
                    }

            var manager = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
            var instance = manager?.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
            Role = Get(Get(instance, "NetService"), "Type")?.ToString() ?? "unknown";
            Emit("combat_start", new { players = Names, role = Role, seed = Get(Get(state, "Rng"), "StringSeed") });
            Hosting.RuntimeHost.Defer(MeterPanel.Ensure);
        });
    }

    public static void EndCombatCompleted()
    {
        Effective.Finish();
        RunSession.Commit();
        RunSession.Active = false;
        Emit("combat_summary", new { totals = Meter.Rows, creditedTotals = Credited.Rows(), sources = Meter.Sources, confirmedChanges = changes.Confirmed, unknownChanges = changes.Unknown, unresolvedCorrelations = changes.Unresolved, health = Health, offensiveCoverage = NativeDamageCapture.Coverage.Outgoing, incomingDiagnostics = NativeDamageCapture.Coverage.Incoming });
        active = false;
        Combat += " complete";
        doom.Clear();
        changes.Clear();
        Flush();
    }

    public static IReadOnlyList<Share> PowerOwnership(PowerModel power) => Ownership(power);
    public static IReadOnlyList<Share> Ownership(PowerModel power) => OwnershipAvailable ? changes.Snapshot(PowerKey(power), power.Amount) : Array.Empty<Share>();
    public static string PowerKey(PowerModel power) => Entity(power.Owner) + ":" + power.GetType().FullName;
    public static void BeforePowerPrefix(PowerModel power, Creature target, Creature? applier, CardModel? cardSource) => Guard(() =>
    {
        if (!active || !OwnershipAvailable)
            return;
        var existing = target.Powers.FirstOrDefault(p => p.GetType() == power.GetType());
        var operation = origin.Current?.OperationId ?? "automatic";
        changes.Begin(operation, Entity(target) + ":" + power.GetType().FullName, existing?.Amount ?? 0, Owner(applier) ?? Owner(cardSource), cardSource?.GetType().Name);
    });
    public static void AfterPowerPrefix(PowerModel power, Creature? applier, CardModel? cardSource) => Guard(() =>
    {
        if (!active || !OwnershipAvailable)
            return;
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

    public static Exception? SourceFinalizer(Exception? __exception, object? __state)
    {
        if (__exception != null && origin.Current != null)
            Guard(() => changes.EndScope(origin.Current.OperationId));
        origin.Current = (Origin?)__state;
        return __exception;
    }

    public static void RemovePrefix(PowerModel power, out string __state) => __state = PowerKey(power);
    public static void DoomPrefix(IEnumerable<Creature> creatures, out object __state)
    {
        var tokens = new Dictionary<Creature, object>();
        __state = tokens;
        Guard(() =>
        {
            foreach (var c in creatures)
            {
                var p = c.GetPower<DoomPower>();
                if (c.CurrentHp > 0 && p != null)
                {
                    var token = new object();
                    tokens[c] = token;
                    doom[c] = (c.CurrentHp, token, Ownership(p));
                }
            }
        });
    }

    public static void CleanDoom(object state) => Guard(() =>
    {
        foreach (var pair in (Dictionary<Creature, object>)state)
            if (doom.TryGetValue(pair.Key, out var entry) && ReferenceEquals(entry.Token, pair.Value))
                doom.Remove(pair.Key);
    });
    public static Exception? DoomFinalizer(Exception? __exception, object __state)
    {
        if (__exception != null && __state != null)
            CleanDoom(__state);
        return __exception;
    }

    public static void PoisonPrefix(PoisonPower __instance, out PowerModel? __state)
    {
        __state = poison.Value;
        poison.Value = __instance;
    }

    public static Exception? PoisonFinalizer(Exception? __exception, PowerModel? __state)
    {
        poison.Value = __state;
        return __exception;
    }

    public static void HandleHook(MethodBase __originalMethod, object[] __args)
    {
        Guard(() =>
        {
            var m = __originalMethod;
            if (!active)
                return;
            if (m.Name == "AfterDamageGiven" && Arg(m, __args, "results") is DamageResult r)
            {
                if (!NativeDamageCapture.Observe(r, out var eventId))
                    return;
                var target = Arg(m, __args, "target") as Creature;
                var dealer = Arg(m, __args, "dealer");
                var card = Arg(m, __args, "cardSource");
                var kind = dealer == null && card == null && poison.Value != null && ReferenceEquals(poison.Value.Owner, target) ? "Poison" : "Direct";
                var owner = Owner(dealer) ?? Owner(card) ?? "Unattributed";
                var normalized = EngineDamage.Normalize(r.UnblockedDamage, r.BlockedDamage, r.OverkillDamage);
                var hp = normalized.Hp;
                var receiver = r.Receiver;
                var shares = kind == "Poison" ? StatusOwnershipLedger.Allocate(Ownership(poison.Value!), hp) : new[]
                {
                    new Share(owner, card?.GetType().Name ?? "Unknown source", hp)
                };
                var damageSource = new SourceIdentity(kind == "Direct" ? "card" : "power", card is CardModel sourceCard ? sourceCard.Id.Entry : kind, card?.GetType().Name ?? kind);
                if (receiver.Side.ToString() == "Enemy" && !Meter.AddResolved(new(r, kind, hp, r.BlockedDamage, shares) { Source = kind == "Direct" ? damageSource : null }))
                    return;
                var amplification = target != null ? NativeDamageCapture.Resolve(target, r, eventId) : null;
                if (receiver.Side.ToString() == "Enemy" && amplification != null)
                {
                    if (kind == "Direct")
                        Meter.ApplyAmplification(owner, amplification, damageSource);
                    else
                        Meter.ApplySharedAmplification(kind, shares, amplification);
                }

                if (receiver.Side.ToString() == "Enemy")
                {
                    var credits = Credited.Add(eventId, kind, hp, normalized.Block, shares, kind == "Direct" ? damageSource : null, amplification, amplification != null);
                    Emit("credited_damage", new { eventId, hp, block = normalized.Block, kind, credits });
                    var bypass = NativeDamageCapture.Bypasses(r);
                    Effective.Add(eventId, Get(receiver, "CombatId")?.ToString(), credits, bypass.HasValue ? hp > 0 && !bypass.Value && receiver.Block == 0 : null);
                    if (Convert.ToDecimal(Get(receiver, "CurrentHp") ?? 1) <= 0 && Get(receiver, "CombatId") is object dead)
                        Effective.Discard(dead.ToString()!);
                    Emit("effective_damage", new { eventId, target = Get(receiver, "CombatId"), bypass, effectiveTotals = Effective.Ledger().Rows(), pending = Effective.Pending.Values.SelectMany(p => p).Sum(c => c.Block), Effective.Incomplete });
                }

                Emit("damage_allocation", new { eventId, target = Entity(target), kind, hp, shares, trigger = origin.Current });
                Emit("damage", new { eventId, originalTarget = Entity(target), receiver = Entity(receiver), target = Entity(target), actor = Entity(dealer), owner, source = card?.GetType().Name ?? poison.Value?.GetType().Name, kind, confidence = shares.Any(s => s.Owner == StatusOwnershipLedger.Unknown) ? "partial_or_unknown" : "observed", reason = shares.Any(s => s.Owner == StatusOwnershipLedger.Unknown) ? "missing history or unresolved correlation" : null, hp = normalized.Hp, rawUnblocked = r.UnblockedDamage, blocked = r.BlockedDamage, overkill = r.OverkillDamage, enemy = receiver.Side.ToString() == "Enemy" });
            }

            if (m.Name == "AfterPowerAmountChanged" && Arg(m, __args, "power") is PowerModel power && power.GetType().Name is "PoisonPower" or "DoomPower" or "VulnerablePower")
            {
                var applier = Arg(m, __args, "applier");
                var delta = Convert.ToDecimal(Arg(m, __args, "amount") ?? 0);
                Emit("power_change", new { effect = power.GetType().Name, target = Entity(power.Owner), after = power.Amount, reportedDelta = delta, applier = Owner(applier), source = Arg(m, __args, "cardSource")?.GetType().Name, shares = Ownership(power) });
            }

            if (m.Name == "AfterDiedToDoom" && Arg(m, __args, "creatures") is IEnumerable victims)
                foreach (var item in victims)
                    if (item is Creature c && doom.Remove(c, out var d))
                    {
                        var shares = StatusOwnershipLedger.Allocate(d.Shares, d.Hp);
                        if (c.Side.ToString() == "Enemy" && !Meter.AddResolved(new(d.Token, "Doom", d.Hp, 0, shares)))
                            continue;
                        var eventId = Guid.NewGuid().ToString("N");
                        if (c.Side.ToString() == "Enemy")
                        {
                            var credits = Credited.Add(eventId, "Doom", d.Hp, 0, shares, null, null);
                            Emit("credited_damage", new { eventId, hp = d.Hp, block = 0, kind = "Doom", credits });
                            Effective.Add(eventId, Get(c, "CombatId")?.ToString(), credits, false);
                            if (Get(c, "CombatId") is object dead)
                                Effective.Discard(dead.ToString()!);
                        }

                        Emit("doom_execution", new { eventId, enemy = c.Side.ToString() == "Enemy", target = Entity(c), hp = d.Hp, shares });
                    }
        });
    }
}
