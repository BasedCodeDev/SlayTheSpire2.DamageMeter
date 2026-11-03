using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
using global::Based.Spires2DamageMeter;
using static global::Based.Spires2DamageMeter.RunSession;

namespace Based.Spires2DamageMeter.Runtime;
internal static class RunSessionLogic
{
    public static void AttachState(object? state)
    {
        if (state == null || ReferenceEquals(currentState, state))
            return;
        if (currentState != null && StateKey(currentState) != StateKey(state))
        {
            generation++;
            store = null;
            identity = null;
            Encounter = "";
            Active = false;
        }

        currentState = state;
    }

    public static string StateKey(object state)
    {
        var players = Probe.Get(state, "Players") as System.Collections.IEnumerable;
        var party = players == null ? "" : string.Join(",", players.Cast<object>().Select(p => Probe.Get(p, "NetId")?.ToString() ?? "").OrderBy(p => p, StringComparer.Ordinal));
        return $"{Probe.Get(state, "StartTime")}|{Probe.Get(Probe.Get(state, "Rng"), "StringSeed")}|{Probe.Get(state, "GameMode")}|{party}";
    }

    public static List<EncounterSnapshot> VisibleEncounters()
    {
        var snapshots = store?.Completed.Values.ToList() ?? new();
        if (Active)
        {
            snapshots.RemoveAll(s => s.Encounter == Encounter);
            snapshots.Add(Capture());
        }

        return snapshots;
    }

    public static List<SourceTotals> SourceBreakdowns() => VisibleEncounters().SelectMany(e => e.StructuredSources).ToList();
    public static List<SupportTransfer> SupportTransfers() => VisibleEncounters().SelectMany(e => e.Transfers).ToList();
    public static CreditedDamage CreditedRun()
    {
        var result = new CreditedDamage();
        foreach (var e in VisibleEncounters())
            if (e.Attribution != null)
                result.Merge(e.Attribution);
        return result;
    }

    public static EffectiveDamageLedger EffectiveRun()
    {
        var result = new EffectiveDamageLedger();
        foreach (var e in VisibleEncounters())
            if (e.Effective != null)
                result.Merge(e.Effective);
            else
                result.Incomplete = true;
        return result;
    }

    public static List<SupportTransfer> CreditedTransfers() => VisibleEncounters().Where(e => e.Attribution?.Version == CreditedDamage.Version).SelectMany(e => e.Transfers).ToList();
    public static EncounterSnapshot Capture() => JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(new EncounterSnapshot(Encounter, Probe.Meter.Rows, Probe.Meter.Sources) { Effective = Probe.Effective.Snapshot(), Transfers = Probe.Meter.Transfers, StructuredSources = Probe.Meter.StructuredSources, Attribution = Probe.Credited.Snapshot() }))!;
    public static void Commit()
    {
        if (store != null && Encounter != "")
            store.Completed[Encounter] = Capture();
    }

    public static Dictionary<string, Dictionary<string, decimal>> RunSources()
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if (Active)
        {
            encounters.RemoveAll(e => e.Encounter == Encounter);
            encounters.Add(Capture());
        }

        foreach (var encounter in encounters)
            foreach (var(id, leaves)in encounter.Sources)
            {
                if (!result.TryGetValue(id, out var target))
                    result[id] = target = new();
                foreach (var(key, value)in leaves)
                    target[key] = target.GetValueOrDefault(key) + value;
            }

        return result;
    }

    public static Dictionary<string, Totals> RunRows()
    {
        var rows = new Dictionary<string, Totals>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if (Active)
        {
            encounters.RemoveAll(e => e.Encounter == Encounter);
            encounters.Add(Capture());
        }

        foreach (var encounter in encounters)
            foreach (var(id, totals)in encounter.Players)
            {
                if (!rows.TryGetValue(id, out var row))
                    rows[id] = row = new();
                row.Hp += totals.Hp;
                row.Block += totals.Block;
                row.Poison += totals.Poison;
                row.Doom += totals.Doom;
                row.AmplifiedHp += totals.AmplifiedHp;
                row.AmplifiedBlock += totals.AmplifiedBlock;
                row.SupportGiven += totals.SupportGiven;
                row.SupportReceived += totals.SupportReceived;
                row.SupportGivenHp += totals.SupportGivenHp;
                row.SupportGivenBlock += totals.SupportGivenBlock;
                row.SupportReceivedHp += totals.SupportReceivedHp;
                row.SupportReceivedBlock += totals.SupportReceivedBlock;
            }

        return rows;
    }

    public static void Error(Exception e)
    {
        storageError = e.Message;
        Probe.Emit("statistics_storage_error", new { error = e.Message });
    }

    public static void SavePrefix(SerializableRun __0, out object? __state)
    {
        __state = saving.Value;
        var operation = new SaveOperation();
        saving.Value = operation;
        try
        {
            operation.Encounters = store?.Completed.Values.ToList() ?? new();
            if (__0.PreFinishedRoom != null && Active && Encounter != "")
            {
                operation.Encounters.RemoveAll(e => e.Encounter == Encounter);
                operation.Encounters.Add(Capture());
            }

            operation.Encounters = JsonSerializer.Deserialize<List<EncounterSnapshot>>(JsonSerializer.Serialize(operation.Encounters))!;
            operation.Partial = store?.Partial == true;
            operation.Warning = store?.Warning;
        }
        catch (Exception e)
        {
            operation.CaptureValid = false;
            Error(e);
        }
    }

    public static void WritePayload(string __0, byte[] __1)
    {
        try
        {
            if (RunSavePolicy.IsRunSave(__0) && saving.Value is { } op)
                op.Payload.Capture(__0, System.Text.Encoding.UTF8.GetString(__1));
        }
        catch (Exception e)
        {
            Error(e);
        }
    }

    public static Exception? SaveFinalizer(Exception? __exception, object? __state)
    {
        saving.Value = (SaveOperation? )__state;
        return __exception;
    }

    public static void Publish(SaveOperation op)
    {
        try
        {
            lock (publicationLock)
            {
                if (op.Generation != generation || op.Order < publishedOrder)
                    return;
                if (!op.CaptureValid)
                    throw new Exception("Statistics snapshot capture failed; checkpoint preserved");
                if (op.Payload.Json == null || op.Payload.Hash == null)
                    throw new Exception("No accepted run-save payload observed");
                var run = PersistencePayload.Identity(op.Payload.Profile, op.Payload.Json);
                if (identity != null && identity != run)
                    op.Encounters.Clear();
                var checkpoint = new RunStatisticsStore(DirectoryPath);
                if (op.Partial)
                    checkpoint.MarkPartial(op.Warning ?? "Partial history");
                foreach (var e in op.Encounters)
                    checkpoint.Completed[e.Encounter] = e;
                checkpoint.Save(run, op.Payload.Hash);
                publishedOrder = op.Order;
                store = checkpoint;
                identity = run;
                Probe.Emit("statistics_checkpoint_saved", new { operationId = op.Order, fingerprintVersion = 2, hash = op.Payload.Hash, encounters = checkpoint.Completed.Count });
            }
        }
        catch (Exception e)
        {
            Error(e);
        }
    }

    public static void LoadPrefix(out object? __state)
    {
        __state = loading.Value;
        loading.Value ??= new();
    }

    public static void ReadPayload(string __0, string __result)
    {
        try
        {
            if (loading.Value is not { } op)
                return;
            if (!RunSavePolicy.IsRunSave(__0)) return;
            // Observe raw I/O only. Never patch a shared generic migration/loader body.
            using var doc = JsonDocument.Parse(__result);
            var root = doc.RootElement;
            if (!root.TryGetProperty("players", out _) || !root.TryGetProperty("rng", out _) || !root.TryGetProperty("start_time", out _))
                return;
            var payload = new PersistencePayload();
            payload.Capture(__0, __result);
            op.Accepted = payload;
        }
        catch (Exception e)
        {
            Error(e);
        }
    }

    public static void LoadPostfix(ReadSaveResult<SerializableRun> __result, object? __state)
    {
        var op = loading.Value;
        loading.Value = (LoadOperation? )__state;
        if (__state != null || __result == null || !__result.Success || __result.SaveData == null)
            return;
        try
        {
            var payload = op?.Accepted ?? throw new Exception("No accepted run-load payload observed");
            var run = PersistencePayload.Identity(payload.Profile, payload.Json!);
            if (!Active && identity == run && loadedHash == payload.Hash)
                return;
            generation++;
            loadedHash = payload.Hash;
            currentState = null;
            identity = run;
            storageError = null;
            store = new(DirectoryPath);
            store.Restore(run, payload.Hash!);
            Encounter = "";
            Active = false;
            Probe.Emit("statistics_checkpoint_loaded", new { fingerprintVersion = 2, hash = payload.Hash, backup = payload.Path?.EndsWith(".backup") == true, encounters = store.Completed.Count, store.Partial, store.Warning });
        }
        catch (Exception e)
        {
            store = null;
            identity = null;
            Encounter = "";
            Active = false;
            Error(e);
        }
    }

    public static Exception? LoadFinalizer(Exception? __exception, object? __state)
    {
        loading.Value = (LoadOperation? )__state;
        return __exception;
    }
}
