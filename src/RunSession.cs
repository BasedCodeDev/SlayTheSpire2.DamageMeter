using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;
namespace Based.Spires2DamageMeter;

public static class RunSession
{
    private static RunStatisticsStore? store;
    private static RunIdentity? identity;
    private static string? storageError;
    private static int generation;
    private static object? currentState;
    public static string Encounter { get; set; } = "";
    public static bool Active { get; set; }
    public static void AttachState(object? state)
    {
        if(state==null || ReferenceEquals(currentState,state))return;
        if(currentState!=null){generation++;store=null;identity=null;Encounter="";Active=false;}
        currentState=state;
    }
    private static List<EncounterSnapshot> VisibleEncounters()
    {
        var snapshots=store?.Completed.Values.ToList()??new();
        if(Active){snapshots.RemoveAll(s=>s.Encounter==Encounter);snapshots.Add(Capture());}
        return snapshots;
    }
    public static List<SourceTotals> SourceBreakdowns()=>VisibleEncounters().SelectMany(e=>e.StructuredSources).ToList();
    public static List<SupportTransfer> SupportTransfers()=>VisibleEncounters().SelectMany(e=>e.Transfers).ToList();
    public static CreditedDamage CreditedRun(){var result=new CreditedDamage();foreach(var e in VisibleEncounters())if(e.Attribution!=null)result.Merge(e.Attribution);return result;}
    public static bool AttributionPartial=>VisibleEncounters().Any(e=>e.Attribution?.Version!=CreditedDamage.Version);
    public static List<SupportTransfer> CreditedTransfers()=>VisibleEncounters().Where(e=>e.Attribution?.Version==CreditedDamage.Version).SelectMany(e=>e.Transfers).ToList();
    public static bool StorageIncomplete=>storageError!=null;
    public static bool HistoryPartial=>store?.Partial==true||identity==null;
    public static string Status => storageError != null ? "Run storage incomplete: " + storageError : store?.Partial == true ? "Partial run: " + store.Warning : identity == null ? "Run history not attached" : "Checkpoint history attached";
    public static EncounterSnapshot Capture() => JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(new EncounterSnapshot(Encounter, Probe.Meter.Rows, Probe.Meter.Sources){Transfers=Probe.Meter.Transfers,StructuredSources=Probe.Meter.StructuredSources,Attribution=Probe.Credited.Snapshot()}))!;
    public static void Commit() { if (store != null && Encounter != "") store.Completed[Encounter] = Capture(); }
    public static Dictionary<string, Dictionary<string, decimal>> RunSources()
    {
        var result = new Dictionary<string, Dictionary<string, decimal>>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if(Active) { encounters.RemoveAll(e=>e.Encounter==Encounter); encounters.Add(Capture()); }
        foreach(var encounter in encounters) foreach(var (id, leaves) in encounter.Sources)
        { if(!result.TryGetValue(id,out var target)) result[id]=target=new(); foreach(var (key,value) in leaves) target[key]=target.GetValueOrDefault(key)+value; }
        return result;
    }
    public static Dictionary<string, Totals> RunRows()
    {
        var rows = new Dictionary<string, Totals>();
        var encounters = store?.Completed.Values.ToList() ?? new List<EncounterSnapshot>();
        if (Active) { encounters.RemoveAll(e => e.Encounter == Encounter); encounters.Add(Capture()); }
        foreach (var encounter in encounters) foreach (var (id, totals) in encounter.Players)
        {
            if (!rows.TryGetValue(id, out var row)) rows[id] = row = new();
            row.Hp += totals.Hp; row.Block += totals.Block; row.Poison += totals.Poison; row.Doom += totals.Doom;
            row.AmplifiedHp += totals.AmplifiedHp; row.AmplifiedBlock += totals.AmplifiedBlock; row.SupportGiven += totals.SupportGiven; row.SupportReceived += totals.SupportReceived;
            row.SupportGivenHp+=totals.SupportGivenHp;row.SupportGivenBlock+=totals.SupportGivenBlock;row.SupportReceivedHp+=totals.SupportReceivedHp;row.SupportReceivedBlock+=totals.SupportReceivedBlock;
        }
        return rows;
    }
    public static void Register()
    {
        var h = new Harmony(Probe.Id + ".RunStorage");
        try
        {
        var save = AccessTools.Method(typeof(RunSaveManager), "SaveRun", new[] { typeof(SerializableRun), typeof(bool) });
        h.Patch(save, postfix: new HarmonyMethod(typeof(RunSession), nameof(SavePostfix)));
        foreach (var name in new[] { "LoadRunSave", "LoadMultiplayerRunSave", "LoadAndCanonicalizeMultiplayerRunSave" })
            h.Patch(AccessTools.Method(typeof(RunSaveManager), name), postfix: new HarmonyMethod(typeof(RunSession), nameof(LoadPostfix)));
        }
        catch(Exception e) { h.UnpatchAll(h.Id); storageError=e.Message; Probe.Emit("statistics_storage_error",new {error=e.ToString()}); }
    }
    private static (RunIdentity Identity, string Hash) Describe(SerializableRun run)
    {
        var json = SaveManager.ToJson(run); using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        var profile = SaveManager.Instance.GetProfileScopedPath("saves");
        var party = string.Join(",", root.GetProperty("players").EnumerateArray().Select(p => p.GetProperty("net_id").ToString()).OrderBy(p => p, StringComparer.Ordinal));
        return (new(profile, run.GameMode.ToString(), run.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture), root.GetProperty("rng").GetProperty("seed").ToString(), party), RunStatisticsStore.Fingerprint(json));
    }
    public static void SavePostfix(SerializableRun __0, ref Task __result) => __result = Saved(__result, __0);
    private static async Task Saved(Task task, SerializableRun run)
    {
        var startedGeneration=generation;
        var captured=store?.Completed.Values.ToList()??new List<EncounterSnapshot>();
        if(run.PreFinishedRoom!=null && Active && Encounter!=""){captured.RemoveAll(e=>e.Encounter==Encounter);captured.Add(Capture());}
        try { if(identity!=null && identity!=Describe(run).Identity)captured.Clear(); }
        catch(Exception e) { storageError=e.Message; captured.Clear(); }
        await task;
        try
        {
            var checkpoint = Describe(run);
            var checkpointStore=new RunStatisticsStore(Path.Combine(OS.GetUserDataDir(),Probe.Id,"checkpoints"));
            if(identity==checkpoint.Identity && store?.Partial==true)checkpointStore.MarkPartial(store.Warning??"Partial history");
            foreach(var snapshot in captured)checkpointStore.Completed[snapshot.Encounter]=snapshot;
            checkpointStore.Save(checkpoint.Identity,checkpoint.Hash);
            if(startedGeneration!=generation)return;
            if (store == null || identity != checkpoint.Identity) { store = new(Path.Combine(OS.GetUserDataDir(), Probe.Id, "checkpoints")); identity = checkpoint.Identity; currentState=null; }
            foreach(var snapshot in captured)store.Completed[snapshot.Encounter]=snapshot;
            Probe.Emit("statistics_checkpoint_saved", new { checkpoint.Hash });
        }
        catch (Exception e) { storageError=e.Message; Probe.Emit("statistics_storage_error", new { error = e.ToString() }); }
    }
    public static void LoadPostfix(ReadSaveResult<SerializableRun> __result)
    {
        if (!__result.Success || __result.SaveData == null) return;
        try
        {
            var checkpoint = Describe(__result.SaveData); identity = checkpoint.Identity;
            generation++;currentState=null;storageError=null;
            store = new(Path.Combine(OS.GetUserDataDir(), Probe.Id, "checkpoints")); store.Restore(identity, checkpoint.Hash);
            Encounter = ""; Active = false; Probe.Emit("statistics_checkpoint_loaded", new { checkpoint.Hash, store.Partial, store.Warning });
        }
        catch (Exception e) { storageError=e.Message; store = null; identity = null; Probe.Emit("statistics_storage_error", new { error = e.ToString() }); }
    }
}
